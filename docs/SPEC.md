# 功能规格

本文档定义 NetStrata 每一轮探测（`collectSample`）采集什么、如何判决。

---

## 1. 探测周期

- 默认间隔：**60 秒**（`NETSTRATA_INTERVAL_MS`）
- 单轮内所有探测项 **并行执行**
- 单轮超时上限：**30 秒**（个别项可独立超时）

---

## 2. 探测项清单

### 2.1 Wi-Fi 链路层 (`wifi`)

**条件**：仅当主网卡为 Wi-Fi 时详细采集；有线网标记 `skipped`。

| 字段 | 类型 | 说明 |
|------|------|------|
| `status` | string | `connected` / `disconnected` / `not_wifi` / `no_interface` |
| `device` | string? | 网卡名 |
| `ssid` | string? | Wi-Fi 名称 |
| `bssid` | string? | AP MAC |
| `channel` | int? | 信道号 |
| `band` | string? | `2.4` / `5` / `6` |
| `rssi` | int? | 信号强度 dBm |
| `noise` | int? | 噪声 dBm |
| `txRate` | int? | 协商速率 Mbps |
| `phyMode` | string? | 802.11 模式 |
| `security` | string? | 加密方式 |

**判决规则**：

| 条件 | 状态 |
|------|------|
| 有线网 / 无 Wi-Fi 接口 | `skipped` |
| 未连接 | `fail` |
| rssi ≤ -80 dBm | `fail` |
| rssi ≤ -70 dBm | `degraded` |
| txRate < 50 Mbps | `degraded` |
| 其他 | `ok` |

---

### 2.2 网络接口 (`iface`)

| 字段 | 类型 | 说明 |
|------|------|------|
| `primaryDevice` | string? | 默认路由网卡 |
| `hardwarePort` | string? | 友好名称（如 Wi-Fi） |
| `linkType` | string | `wifi` / `ethernet` / `other` / `unknown` |
| `ipv4` | string? | 本机 IPv4 |
| `gateway` | string? | 默认网关 |
| `subnetMask` | string? | 子网掩码 |
| `dhcpServer` | string? | DHCP 服务器 |
| `dhcpDns` | string[] | DHCP 分配的 DNS |

---

### 2.3 局域网 Ping (`pings` → gateway)

- **目标**：`iface.gateway`（若存在）
- **参数**：3 包，超时 1500ms/包
- **指标**：`lossPct`, `minMs`, `avgMs`, `maxMs`, `stddevMs`

**判决**：

| 条件 | 状态 |
|------|------|
| 无网关 | `unknown` |
| loss = 100% | `fail` |
| avgMs > 30 | `degraded` |
| 其他 | `ok` |

---

### 2.4 公网 Ping (`pings`)

固定目标（始终探测）：

```
223.5.5.5    # 阿里 DNS
119.29.29.29 # 腾讯 DNS
1.1.1.1      # Cloudflare
8.8.8.8      # Google DNS
```

用于 **broadband** 层判决（以 `223.5.5.5` 为主）。

---

### 2.5 DNS 解析 (`dns`)

**服务器 × 域名矩阵**（共 20 条）：

```
Servers: system, 223.5.5.5, 119.29.29.29, 8.8.8.8, 1.1.1.1
Domains: baidu.com, google.com, github.com, cloudflare.com
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `server` | string | DNS 服务器 |
| `domain` | string | 查询域名 |
| `ok` | bool | 是否成功 |
| `ms` | double | 耗时 |
| `ips` | string[] | 解析结果 |
| `flags` | string? | 状态码 |
| `err` | string? | 错误信息 |

**broadband 层关键项**：`dig baidu.com @223.5.5.5` 等价检测。

---

### 2.6 HTTPS 探测 (`https`)

#### 直连（始终执行）

| label | URL | 备注 |
|-------|-----|------|
| `baidu_direct` | `https://www.baidu.com` | 国内 |
| `taobao_direct` | `https://www.taobao.com` | 国内 |
| `google_direct` | `https://www.google.com` | 国际 |
| `cloudflare_direct` | `https://www.cloudflare.com` | 国际 |
| `github_direct` | `https://github.com` | 国际 |
| `anthropic_direct` | `https://api.anthropic.com/` | AI，`acceptAnyCode` |
| `openai_direct` | `https://api.openai.com/` | AI，`acceptAnyCode` |

#### 代理（仅检测到代理时追加）

| label | URL |
|-------|-----|
| `google_proxy` | `https://www.google.com` |
| `cloudflare_proxy` | `https://www.cloudflare.com` |
| `github_proxy` | `https://github.com` |
| `youtube_proxy` | `https://www.youtube.com` |
| `anthropic_proxy` | `https://api.anthropic.com/` |
| `openai_proxy` | `https://api.openai.com/` |

#### 每条记录字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `label` | string | 唯一标识 |
| `url` | string | 目标 URL |
| `via` | string | `direct` / `proxy` |
| `ok` | bool | 是否成功 |
| `httpCode` | int | HTTP 状态码 |
| `remoteIp` | string | 远端 IP |
| `dnsMs` | double | DNS 耗时 |
| `connectMs` | double | TCP 连接耗时 |
| `tlsMs` | double | TLS 握手耗时 |
| `firstByteMs` | double | 首字节耗时 |
| `totalMs` | double | 总耗时 |
| `timedOut` | bool | 是否超时 |
| `err` | string? | 错误 |

#### 成功判定

- **普通站点**：HTTP 200/204/301/302/401/403
- **AI 端点**（`acceptAnyCode`）：任意 HTTP 响应码 > 0 且未超时
- **直连探测**：必须禁用系统代理
- **代理探测**：必须走指定代理 URL
- **超时**：默认 8 秒

---

### 2.7 代理配置 (`proxyConfig`)

| 字段 | 类型 | 说明 |
|------|------|------|
| `proxyUrl` | string? | 如 `http://127.0.0.1:7890` |
| `proxyPort` | int? | 端口 |
| `envHttp` | string? | 环境变量 http_proxy |
| `envHttps` | string? | 环境变量 https_proxy |
| `envAll` | string? | 环境变量 all_proxy |
| `systemProxy` | object | Windows 系统代理设置（见 DATA-MODEL） |
| `listening` | bool | 端口是否在 LISTEN |
| `listenerProcess` | string? | 监听进程名 |

**代理 URL 检测优先级**：

1. `NETSTRATA_PROXY` 环境变量（`none`/`off` = 禁用）
2. `https_proxy` / `http_proxy` / `all_proxy` 环境变量
3. Windows 系统代理（注册表 Internet Settings）
4. WinHTTP 代理（`netsh winhttp show proxy`）
5. 常见本地端口扫描 fallback：`7890`, `7897`, `10809`, `1080`（可选，Phase 2）

---

### 2.8 代理出口 IP (`proxyEgress`)

通过代理访问：

1. `https://api.ipify.org`
2. `https://ifconfig.me/ip`（fallback）

| 字段 | 类型 | 说明 |
|------|------|------|
| `ok` | bool | |
| `ip` | string? | 出口公网 IP |
| `ms` | double | 耗时 |
| `err` | string? | |

---

### 2.9 Captive Portal (`captive`)

- URL：`http://captive.apple.com/hotspot-detect.html`
- 成功：HTTP 200 且 body 含 `Success`
- 用于检测酒店/机场强制门户

---

### 2.10 代理下载测速 (`proxyDownload`)

- 每 `NETSTRATA_DOWNLOAD_EVERY` 轮执行一次（默认每 10 轮）
- URL：`https://speed.cloudflare.com/__down?bytes=5000000`
- 通过代理下载 5MB，计算 Mbps

---

### 2.11 Tailscale（可选，Phase 4）

| 字段 | 说明 |
|------|------|
| `installed` | 服务/进程是否存在 |
| `signedIn` | 是否已登录 |
| `exitNodeActive` | 是否使用 exit node |
| `address` | Tailscale IP |

---

## 3. 分层判决（`judge`）

6 层 + 总体 verdict + AI 独立 headline。详见 [DATA-MODEL.md](DATA-MODEL.md#verdict-判决)。

### 层依赖关系

```
wifi → lan → broadband → overseas_direct
                              ↓
                           proxy → ai
```

- `broadband fail` → `overseas_direct` = `skipped`
- `lan fail` → `broadband` = `skipped`
- 无代理配置 → `proxy` = `skipped`（不是 fail）

### 总体 verdict（`overall`）

优先级从高到低：

| overall | 触发条件 |
|---------|----------|
| `wifi_bad` | wifi = fail |
| `lan_bad` | lan = fail |
| `broadband_bad` | broadband = fail |
| `proxy_bad` | proxy = fail |
| `direct_blocked_proxy_ok` | overseas = fail 且 proxy = ok |
| `degraded` | 任一层 degraded |
| `healthy` | 全绿 |

---

## 4. 运行模式

| 模式 | 参数 | 行为 |
|------|------|------|
| Once | `--once` | 单次探测，stdout 输出 JSON，退出 |
| Web | `--web` | 启动 HTTP 服务 + 后台 Daemon 循环探测 |
| TUI | （默认） | 终端面板；若 Daemon 已运行则跟随 `samples.jsonl` |

### TUI 快捷键（规划）

| 键 | 动作 |
|----|------|
| `q` | 退出 |
| `l` | 切换语言 zh/en |
| `r` | 立即刷新 |

---

## 5. 持久化

| 文件 | 格式 | 说明 |
|------|------|------|
| `data/samples.jsonl` | JSONL | 每行一个 Sample，追加写入 |
| `data/state.json` | JSON | 最新状态 + 滚动统计 |
| `data/conclusions.md` | Markdown | 周期性结论（可选，后期） |
| `logs/daemon.log` | 文本 | Daemon 日志 |

默认数据目录：`%APPDATA%\NetStrata\` 或当前工作目录 `./data/`（实现时二选一，文档先约定 `%APPDATA%\NetStrata\`）。
