# 技术债务追踪

本文档记录 ROADMAP 技术债务项的**现状、处理决策与验证方式**。实现变更时同步更新。

---

## 状态总览

| 项 | 状态 | 说明 |
|----|------|------|
| HTTPS 时序分段 | **已改善** | `dnsMs` / `connectMs` / `tlsMs` / `firstByteMs` 由 ConnectCallback + 分段计时填充 |
| SOCKS5 代理 | **已支持（基础）** | 环境变量、`socks=` 注册表、1080 端口 socks5 回退 |
| IPv6 支持 | **已支持（基础）** | `iface.ipv6`、IPv6 ping 目标校验 |
| 多网卡 / VPN 干扰 | **已改善** | VPN/Tailscale 网卡降权 + `routeHints` |
| 前端 systemProxy | **已补齐** | 仪表盘展示 `proxyConfig` / `systemProxy` |
| TLS 探测 Integration | **已隔离** | `[Trait("Category","Integration")]`，默认 `dotnet test` 跳过 |
| 自定义 ping 滥用 | **已加固** | 校验 + 上限 10 + 标签 + 不参与 verdict |

---

## 1. HTTPS 时序分段

**问题**：早期仅填充 `totalMs`，`dnsMs` 等字段为 0。

**处理**（`HttpsProbe`）：

- `dnsMs`：请求前 `Dns.GetHostAddressesAsync` 计时
- `connectMs`：`SocketsHttpHandler.ConnectCallback` TCP 连接计时
- `firstByteMs`：至 `ResponseHeadersRead` 的耗时
- `tlsMs`：`max(0, firstByteMs - connectMs)`（ponytail：HttpClient 不暴露独立 TLS 握手时刻）
- `totalMs`：整次探测墙钟时间

**局限**：TLS 与 TTFB 仍合并在 `firstByteMs` 主路径中；精确 DPI 诊断用 `TlsStackProbe`。

**验证**：`HttpsProbeTests.Timing_PopulatesSegmentFields`（mock ConnectCallback 路径）

---

## 2. SOCKS5 代理

**问题**：仅 HTTP 代理 `http://127.0.0.1:port`。

**处理**（`ProxyDetector` + `WindowsRegistryProxyReader`）：

- 识别 `socks5://` / `socks://` 环境变量（`ALL_PROXY`、`SOCKS_PROXY`）
- 注册表 `ProxyServer` 中 `socks=host:port` 段 → `SocksEnable`
- 端口 **1080** 监听且无 HTTP 代理时 → `socks5://127.0.0.1:1080`
- `WebProxy` / `HttpClient` 使用 socks URI（.NET 内置）

**局限**：无 SOCKS 认证；混合 PAC 自动配置未实现。

**验证**：`ProxyDetectorTests.Detect_SocksEnv` / `Detect_RegistrySocks`

---

## 3. IPv6 支持

**问题**：仅 IPv4 接口与 ping。

**处理**：

- `InterfaceInfo.ipv6`：首个非 link-local IPv6 单播地址
- `PingTargetValidator` 接受 IPv6 字面量
- DNS / HTTPS 沿用系统双栈

**局限**：未单独探测 IPv6-only 路径；网关仍以 IPv4 为主。

---

## 4. 多网卡 / VPN 干扰

**问题**：`InterfaceProbe` 可能选中 Tailscale/WireGuard 为「主接口」。

**处理**：

- 排序时 **VPN 虚拟网卡降权**（Tailscale、Wintun、WireGuard、TAP）
- `RouteHintDetector`：`multiple default routes` / `tailscale interface present`
- 写入 `iface.routeHints`

**局限**：未读取 Windows 路由表 metric；极端多路由环境仍建议人工看 `routeHints`。

---

## 5. 前端 systemProxy 字段

**问题**：canireach 前端展示 `systemProxy`（scutil），NetStrata 用注册表但未展示。

**处理**：`web/index.html` 增加代理摘要行（`proxyUrl`、`listening`、`systemProxy.http/https/socks`）。

---

## 6. Layer 3 TLS 探测精度

**问题**：单元测试 mock 栈；真实环境需联网验证。

**处理**：

- 默认测试集：`TlsStackProbeTests`（mock）
- 集成：`TlsStackIntegrationTests`，Trait `Integration`，**默认 CI/Agent 不跑**
- 手动：`dotnet test --filter "Category=Integration"`

---

## 7. 自定义 ping 滥用

**问题**：需防止过多/非法目标影响探测周期。

**处理**：

- `PingTargetValidator`：IP/主机名校验，非法跳过
- `MergePingExtra` + `Filter`：最多 **10** 个
- `config.json` → `pingExtraLabels` 写入 `PingResult.label`
- **不参与** `VerdictEngine` 六层判决

**验证**：`PingTargetValidatorTests`、`NetStrataOptions` merge 测试

---

## 默认测试过滤

`tests/NetStrata.Core.Tests` 默认排除 Integration：

```xml
<VSTestTestCaseFilter>Category!=Integration</VSTestTestCaseFilter>
```

全量含集成：

```powershell
dotnet test --filter "Category=Integration"
dotnet test  # 无 filter 属性时跑全部 — 见 csproj
```

---

## 仍开放（按需）

| 项 | 说明 |
|----|------|
| `NetStrata.Tray` WPF 壳 | `TrayStatusMapper` 已就绪 |
| 专用 MCP Server | 见 [SCHEDULING.md](SCHEDULING.md) |
| HTTPS 精确 TLS 握手毫秒 | 需底层 socket 或 ETW |
| Windows 路由 metric | 需 `GetIpForwardTable` P/Invoke |
| SOCKS 认证 | 企业代理场景 |
