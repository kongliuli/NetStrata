# 第三层：增强能力规格

本文档定义 NetStrata 在 Phase 1–4 基础产品完成后的 **Layer 3** 目标：从「能用的监控工具」升级为「Windows 分层网络诊断参考实现」。

**原则**：每项能力先有 [TESTING.md](TESTING.md) 测试用例，再实现；数据模型变更同步 [DATA-MODEL.md](DATA-MODEL.md)。

---

## 能力总览

| 能力 | Phase | 用户价值 |
|------|-------|----------|
| 自定义 Ping 目标 | **2.5**（提前） | 监控内网设备、特定节点 |
| TLS/SNI 分层探测 | 5a | 区分 DNS 污染 / TCP RST / TLS DPI |
| 路由/出口变化告警 | 5b | 网关或代理 IP 突变时提示 |
| 代理配置漂移检测 | 2（已有逻辑）+ 5b 强化 | 系统代理端口 ≠ 实际监听 |
| 多网卡 / VPN 干扰提示 | 5b | Tailscale/WireGuard 抢路由 |
| 结论规则引擎 | 5c | 周期性 Markdown 人类可读报告 |
| 报告导出 | 5c | 分享最近 1h 诊断 |
| 系统托盘（可选） | 5d | 常驻 + 图标状态 |

Phase 编号：5a–5d 合称 **Phase 5（Layer 3）**，在 ROADMAP 中展开。

---

## 1. 自定义 Ping 目标（Phase 2.5）

### 动机

内置公网 IP（223.5.5.5 等）只能判断宽带层；用户常需监控：
- 内网 NAS / 路由器 / 打印机
- 公司 VPN 内网节点
- 特定 CDN / 游戏服务器 IP

### 配置来源（优先级从高到低）

1. **CLI 单次**：`netstrata --once --ping 192.168.1.50,10.0.0.1`
2. **环境变量**：`NETSTRATA_PING_EXTRA=192.168.1.50,10.0.0.1`
3. **配置文件**：`%APPDATA%\NetStrata\config.json`

```json
{
  "pingExtra": ["192.168.1.50", "10.0.0.1", "nas.local"],
  "pingExtraLabels": {
    "192.168.1.50": "nas",
    "10.0.0.1": "vpn-peer"
  }
}
```

### 行为

- 与内置目标 **并行** ping（同样 3 包、1500ms 超时）
- 合并进 `sample.pings[]`，额外字段 `custom: true`
- 可选 `label`（来自 `pingExtraLabels` 或默认用 target 字符串）
- **不影响 6 层 verdict**（纯观测）；Web/TUI 单独区域展示「自定义目标」
- **上限 10 个** extra 目标（防滥用）；非法 IP/主机名跳过并写 `logs/daemon.log` warn

### Series 图表

`GET /api/series` 的 `pings` 对象增加动态键：

```json
"pings": {
  "gw": [2.1, null],
  "ali": [5.1, 5.3],
  "custom_nas": [1.2, 1.5],
  "custom_vpn-peer": [45.0, null]
}
```

键名规则：`custom_{label}`，label 非字母数字则 sanitize 为 target 的 hash 前 8 位。

---

## 2. TLS/SNI 分层探测（Phase 5a）

参考 [rkn-block-checker](https://github.com/MayersScott/rkn-block-checker) 的 DNS→TCP→TLS→HTTP 模型，对 **选定目标** 做栈级诊断。

### 探测目标（默认 3 个，可配置）

| label | host | 说明 |
|-------|------|------|
| `stack_google` | `www.google.com:443` | 国际典型 |
| `stack_github` | `github.com:443` | 开发者常用 |
| `stack_anthropic` | `api.anthropic.com:443` | AI API |

配置：`config.json` → `tlsStackTargets: ["github.com"]` 覆盖默认。

### 每层探测

| 层 | 方法 | 失败含义 |
|----|------|----------|
| `dns` | DnsClient 指定 system + 223.5.5.5 | 解析失败 / 仅 system 失败 → 可能 DNS 污染 |
| `tcp` | `TcpClient.ConnectAsync(host, 443)` | RST/超时 → IP 级封锁 |
| `tls` | `SslStream` 握手 + SNI=host | TCP ok 但 TLS 失败 → **DPI/TSPU 典型指纹** |
| `http` | TLS 后 `GET /` | 451 / 运营商 stub 页 |

### 数据结构

见 [DATA-MODEL.md](DATA-MODEL.md#tlsstackresult)。

### 与 verdict 关系

- **不修改** `overall` 六层优先级
- 在 `sample.alerts` 或 `verdict.insights[]` 追加信息性条目，例如：
  - `tls_block:github.com` — 「github TLS 在 TCP 成功后失败，疑似 SNI 干扰」

---

## 3. 路由与出口变化告警（Phase 5b）

### RouteWatch

Daemon 每轮对比上一轮 `Sample`：

| 事件 | 条件 | alert.type |
|------|------|------------|
| 网关变化 | `iface.gateway` 变化 | `gateway_changed` |
| 本机 IP 变化 | `iface.ipv4` 变化 | `ipv4_changed` |
| 代理出口变化 | `proxyEgress.ip` 变化 | `egress_changed` |
| 默认网卡变化 | `iface.primaryDevice` 变化 | `interface_changed` |

### 输出

```json
"alerts": [
  {
    "t": "2026-07-09T04:00:00Z",
    "type": "egress_changed",
    "message": "proxy egress 1.2.3.4 → 5.6.7.8",
    "prev": "1.2.3.4",
    "curr": "5.6.7.8"
  }
]
```

- 写入 `state.json` 的 `recentAlerts`（保留最近 20 条）
- Web 仪表盘顶部横幅展示未读 alert
- TUI 一行摘要

### 多网卡 / VPN 提示

`InterfaceProbe` 扩展：若存在多条 `0.0.0.0/0` 路由或 `100.64.0.0/10`（Tailscale）地址：

```json
"iface": {
  "routeHints": [
    "multiple default routes detected",
    "tailscale interface utun present"
  ]
}
```

不自动 fail，仅 `routeHints` + 可能 `verdict.headline` 追加说明。

---

## 4. 代理配置漂移（Phase 5b 强化）

Phase 2 已在 `VerdictEngine` 检测：注册表 HTTP 端口 ≠ `proxyConfig.proxyPort`。

Layer 3 追加：
- **连续 3 轮** `listening=false` 但 `proxyUrl` 已配置 → alert `proxy_down`
- **连续 3 轮** egress IP 与首轮不同且间隔 < 5min → alert `egress_flapping`（节点不稳定）

---

## 5. 结论规则引擎（Phase 5c）

### GET /api/conclusions

基于最近 N（默认 60）条 `samples.jsonl`，规则引擎生成 Markdown。

### 规则示例（纯函数，可单测）

| 规则 ID | 条件 | 输出片段 |
|---------|------|----------|
| `R01` | last20 overall 中 degraded ≥ 10 | 「过去 20 轮中网络多次降级」 |
| `R02` | wifi fail ≥ 3 | 「Wi-Fi 信号不稳定」 |
| `R03` | proxy ok 且 overseas fail 稳定 | 「国际直连被屏蔽，代理工作正常（预期）」 |
| `R04` | ai.state == fail ≥ 5 | 「AI API 持续不可达，请检查代理」 |
| `R05` | custom ping `nas` fail ≥ 3 | 「自定义目标 nas (192.168.1.50) 不可达」 |
| `R06` | tls_block 出现 ≥ 2 | 「检测到 TLS/SNI 层阻断」 |

实现：`ConclusionEngine.Generate(IReadOnlyList<Sample>) → string`（Markdown）。

周期性：Daemon 每 `NETSTRATA_CONCLUSION_EVERY` 轮（默认 30）重写 `data/conclusions.md`。

---

## 6. 报告导出（Phase 5c）

### CLI

```powershell
netstrata --export --minutes 60 --format markdown -o report.md
netstrata --export --minutes 60 --format json -o report.json
```

### API

`GET /api/export?minutes=60&format=json`

内容：时间范围、overall 分布、各层状态统计、custom ping 摘要、recent alerts、conclusions 节选。

---

## 7. 系统托盘（Phase 5d，可选）

- **WPF** 或 **WinForms** 薄壳项目 `NetStrata.Tray`
- 读取 `state.json`，图标颜色映射 `overall`
- 右键：打开仪表盘、立即探测、退出
- 开机自启：快捷方式写入 Startup 文件夹（用户确认，不默认开启）

**测试**：托盘逻辑抽 `TrayStatusMapper` 单测；不启动真实托盘进程。

---

## 8. 收手线

Layer 3 完成后 **不再扩展**：
- 远程 SaaS / 多机监控
- 自动切换代理节点
- 完整 Wireshark 级分析

维护重心：修 bug、适配新代理客户端进程名、社区贡献的 `pingExtra` / `tlsStackTargets` 预设。

---

## 文档索引

| 文档 | Layer 3 相关章节 |
|------|------------------|
| [SPEC.md](SPEC.md) | 2.4 自定义 Ping、2.12 TLS 栈 |
| [DATA-MODEL.md](DATA-MODEL.md) | PingResult.custom、TlsStackResult、Alert |
| [ARCHITECTURE.md](ARCHITECTURE.md) | TlsProbe、RouteWatch、ConclusionEngine |
| [ROADMAP.md](ROADMAP.md) | Phase 2.5、Phase 5 |
| [TESTING.md](TESTING.md) | Layer 3 测试表 |
| [API.md](API.md) | /api/export、/api/conclusions |
