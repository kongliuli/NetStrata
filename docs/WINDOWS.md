# Windows 平台实现

本文档将 canireach 的 macOS 探测方式映射为 Windows/.NET 实现。

---

## 总览对照表

| 探测项 | canireach (macOS) | NetStrata (Windows) |
|--------|-------------------|---------------------|
| Wi-Fi 信息 | `system_profiler SPAirPortDataType` | `netsh wlan show interfaces` |
| 默认路由/网关 | `route -n get default` | `Get-NetRoute` / `RoutePrint` |
| 本机 IP | `ipconfig getsummary` | `NetworkInterface.GetIPProperties()` |
| Ping | `/sbin/ping -c 3` | `System.Net.NetworkInformation.Ping` |
| DNS | `/usr/bin/dig @server` | `DnsClient` NuGet `LookupClient` |
| HTTPS | `/usr/bin/curl -w '%{time_*}'` | `HttpClient` + `SocketsHttpHandler` |
| 系统代理 | `scutil --proxy` | 注册表 `Internet Settings` |
| WinHTTP 代理 | — | `netsh winhttp show proxy` |
| 端口监听 | `lsof -iTCP:port` | `IPGlobalProperties.GetActiveTcpListeners()` |
| 监听进程 | `lsof` 进程名 | `Get-NetTCPConnection` → PID → 进程名 |
| 出口 IP | `curl -x proxy ipify` | `HttpClient` + `WebProxy` |
| Captive Portal | `curl captive.apple.com` | `HttpClient` |
| Tailscale | `ps` + `ifconfig utun` | 检测 `Tailscale` 服务 + `100.64.0.0/10` 地址 |

---

## 1. Wi-Fi 信息

### 方案 A：`netsh`（推荐 Phase 2）

```powershell
netsh wlan show interfaces
```

解析字段：
- `SSID` → ssid
- `BSSID` → bssid
- `Signal` → rssi（百分比，需转换为 dBm 估算：`dBm ≈ Signal/2 - 100`）
- `Channel` → channel
- `Radio type` → phyMode
- `Authentication` → security
- `Receive rate (Mbps)` → txRate

### 方案 B：Native WiFi API

`wlanapi.dll` P/Invoke，更精确但复杂度高。Phase 3+ 考虑。

### 有线网判定

```csharp
var ni = NetworkInterface.GetAllNetworkInterfaces()
    .First(n => n.OperationalStatus == OperationalStatus.Up
             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
var linkType = ni.NetworkInterfaceType switch
{
    NetworkInterfaceType.Wireless80211 => "wifi",
    NetworkInterfaceType.Ethernet => "ethernet",
    _ => "other"
};
```

---

## 2. 网关与接口

```csharp
// 获取默认网关
var props = ni.GetIPProperties();
var gateway = props.GatewayAddresses
    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
    ?.Address.ToString();
```

多网卡时选 **Metric 最小** 的默认路由（`Get-NetRoute -DestinationPrefix '0.0.0.0/0'`）。

---

## 3. Ping

```csharp
public async Task<PingResult> PingAsync(string target, int count = 3)
{
    using var ping = new Ping();
    // 发 count 次，统计 loss/avg/min/max
}
```

### Windows ICMP 被禁问题

现象：ping 全失败，但 HTTPS 正常。

**判决修正**（NetStrata 扩展）：

```
if (ping.fail && baidu_https.ok) {
    broadband.state = "degraded"  // 而非 fail
    broadband.reasons.Add("ping blocked (likely firewall), https ok")
}
```

---

## 4. DNS

NuGet：`DnsClient` (v1.x)

```csharp
// 指定 DNS 服务器
var endpoint = new IPEndPoint(IPAddress.Parse("223.5.5.5"), 53);
var client = new LookupClient(endpoint);
var result = await client.QueryAsync("baidu.com", QueryType.A, cancellationToken: ct);
```

系统 DNS：

```csharp
var client = new LookupClient(); // 使用系统配置
```

---

## 5. HTTPS

### 直连（禁用代理）

```csharp
var handler = new SocketsHttpHandler { UseProxy = false };
```

**必须**清除环境变量代理对 HttpClient 的影响（`UseProxy=false` 已足够）。

### 走代理

```csharp
var handler = new SocketsHttpHandler
{
    Proxy = new WebProxy(proxyUrl),
    UseProxy = true,
};
```

### HEAD 请求

```csharp
var request = new HttpRequestMessage(HttpMethod.Head, url);
var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
```

AI 端点 `acceptAnyCode`：任意 status code > 0 即 `ok`。

---

## 6. 系统代理检测

### 注册表（用户级代理）

路径：

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings
```

| 值名 | 类型 | 映射 |
|------|------|------|
| `ProxyEnable` | DWORD | httpEnable |
| `ProxyServer` | string | `host:port` 或 `http=host:port;https=host:port` |
| `ProxyOverride` | string | bypassList |

```csharp
using var key = Registry.CurrentUser.OpenSubKey(
    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
var enabled = (int)(key?.GetValue("ProxyEnable") ?? 0) == 1;
var server = key?.GetValue("ProxyServer") as string;
```

### WinHTTP 代理（系统级，部分应用使用）

```powershell
netsh winhttp show proxy
```

解析输出中的 `Proxy Server(s)` 行。可用 `WinHttpGetProxyForUrl` P/Invoke 替代。

---

## 7. 端口监听 + 进程识别

```csharp
var listeners = IPGlobalProperties.GetIPGlobalProperties()
    .GetActiveTcpListeners();
var listening = listeners.Any(ep => ep.Port == proxyPort);
```

进程名：

```powershell
Get-NetTCPConnection -LocalPort 7890 -State Listen |
  Select-Object OwningProcess
```

```csharp
var proc = Process.GetProcessById(pid);
var name = proc.ProcessName; // e.g. "verge-mihomo", "clash-win64"
```

常见代理进程名：

| 进程名 | 客户端 |
|--------|--------|
| `verge-mihomo` | Clash Verge Rev |
| `clash-win64` | Clash for Windows |
| `mihomo` | mihomo 核心 |
| `v2rayN` | V2RayN |
| `sing-box` | sing-box |

---

## 8. TUN 模式处理

Clash TUN / sing-box TUN 模式下：
- 系统代理可能 **未启用**
- 流量仍走代理

**策略**：

1. 用户手动设置 `NETSTRATA_PROXY=http://127.0.0.1:7890`
2. Phase 2 可选：扫描常见本地端口，对响应 HTTP CONNECT 的端口自动识别
3. 读取 Clash 配置文件 `mixed-port` / `port`（`%USERPROFILE%\.config\clash\config.yaml`）

---

## 9. 自动打开浏览器

canireach 在 Windows 上用 `start` 命令会崩溃。

```csharp
if (!options.NoOpen)
{
    Process.Start(new ProcessStartInfo
    {
        FileName = $"http://localhost:{options.Port}",
        UseShellExecute = true,
    });
}
```

或用 `NETSTRATA_NO_OPEN=1` 跳过。

---

## 10. 数据目录

```
%APPDATA%\NetStrata\
```

```csharp
var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "NetStrata", "data");
```

避免 canireach 的 `import.meta.url.pathname` 在 Windows 上的路径 bug。

---

## 11. 常见代理端口

扫描 fallback 列表（仅当无其他来源时）：

```
7890   # Clash 默认 HTTP
7897   # Clash Verge Rev mixed
10809  # V2RayN
1080   # SOCKS 常见
7891   # Clash SOCKS
```

检测方式：TCP connect 127.0.0.1:port，不发送数据，能连上即 `listening=true`。

---

## 12. 中英文

- 环境变量 `NETSTRATA_LANG=zh|en`
- 默认：系统 UI culture 以 `zh` 开头 → 中文
- TUI / Web 前端维护 i18n 字符串表
