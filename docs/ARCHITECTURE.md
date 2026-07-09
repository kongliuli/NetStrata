# 架构设计

## 解决方案结构

```
NetStrata/
├── src/
│   ├── NetStrata.Cli/                 # 入口：解析参数，分发模式
│   ├── NetStrata.Core/                # 核心业务逻辑（无 UI 依赖）
│   │   ├── Probes/                    # 各探测项实现
│   │   ├── Judge/                     # 分层判决引擎
│   │   ├── Collector/                 # 并行采集调度
│   │   ├── Models/                    # 数据模型
│   │   ├── Config/                    # 环境变量 / 选项
│   │   └── Storage/                   # jsonl / state 读写
│   ├── NetStrata.Daemon/              # 后台循环探测 HostedService
│   └── NetStrata.Web/                 # ASP.NET Core Minimal API + 静态文件
├── tests/
│   └── NetStrata.Core.Tests/          # 单元测试（mock 探测，不真弹窗）
├── docs/                              # 本文档集
├── web/                               # 前端静态资源（移植 canireach 或重写）
├── NetStrata.sln
└── README.md
```

---

## 模块依赖

```mermaid
flowchart TB
    Cli[NetStrata.Cli] --> Core[NetStrata.Core]
    Cli --> Daemon[NetStrata.Daemon]
    Cli --> Web[NetStrata.Web]
    Daemon --> Core
    Web --> Core
    Web --> Daemon
```

**依赖规则**：
- `Core` 不依赖 `Web` / `Cli` / `Daemon`
- 所有探测逻辑在 `Core`，便于单元测试
- `Daemon` 仅负责定时调用 `SampleCollector` + 持久化

---

## 核心接口

### IProbe\<T\>

```csharp
public interface IProbe<T>
{
    string Name { get; }
    Task<T> ProbeAsync(CancellationToken ct);
}
```

### 探测实现清单

| 类 | 返回类型 | Phase |
|----|----------|-------|
| `WifiProbe` | `WifiInfo` | 2 |
| `InterfaceProbe` | `InterfaceInfo` | 1 |
| `PingProbe` | `IReadOnlyList<PingResult>` | 1 |
| `DnsProbe` | `IReadOnlyList<DnsResult>` | 1 |
| `HttpsProbe` | `IReadOnlyList<HttpsResult>` | 1 |
| `ProxyConfigProbe` | `ProxyConfig` | 2 |
| `ProxyEgressProbe` | `ProxyEgress?` | 2 |
| `CaptiveProbe` | `CaptiveResult` | 3 |
| `ProxyDownloadProbe` | `ProxyDownload?` | 3 |
| `TailscaleProbe` | `TailscaleInfo` | 4 |

### SampleCollector

```csharp
public sealed class SampleCollector
{
    public async Task<Sample> CollectAsync(
        CollectOptions options,
        CancellationToken ct);
}
```

职责：
1. 调用 `InterfaceProbe` 获取网关
2. 并行调度所有 Probe
3. 调用 `VerdictEngine.Judge(sample)` 附加判决
4. 返回完整 `Sample`

### VerdictEngine

```csharp
public sealed class VerdictEngine
{
    public Verdict Judge(Sample sample);
}
```

纯函数，无 I/O。逻辑移植自 canireach `judge()`，见 [SPEC.md](SPEC.md#3-分层判决judge)。

---

## HTTP 探测设计

### 直连 HttpClient

```csharp
var handler = new SocketsHttpHandler
{
    UseProxy = false,           // 关键：禁用系统代理
    ConnectTimeout = TimeSpan.FromSeconds(8),
    AutomaticDecompression = DecompressionMethods.All,
};
var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
```

### 代理 HttpClient

```csharp
var handler = new SocketsHttpHandler
{
    Proxy = new WebProxy("http://127.0.0.1:7890"),
    UseProxy = true,
};
```

### 时序分段

用 `Stopwatch` 或 `Activity` 记录：
- DNS → Connect → TLS → FirstByte → Total

若精确分段成本高，Phase 1 可只记录 `totalMs`，Phase 2 再细化。

---

## Ping 探测设计

```csharp
using var ping = new Ping();
var reply = await ping.SendPingAsync(target, timeoutMs: 1500);
```

注意：
- Windows 防火墙可能禁 ICMP → 记录 `err`，判决时 HTTPS 正常则 broadband 不应仅凭 ping fail
- Phase 2 增加 **「ping 失败但 HTTPS 成功 → degraded 而非 fail」** 的 Windows 特化逻辑（见 [WINDOWS.md](WINDOWS.md)）

---

## DNS 探测设计

**推荐**：`DnsClient` NuGet 包（`LookupClient`）指定 nameserver。

```csharp
var client = new LookupClient(IPAddress.Parse("223.5.5.5"));
var result = await client.QueryAsync("baidu.com", QueryType.A);
```

`system` 服务器：使用系统默认 DNS（`LookupClient` 不传 nameserver）。

---

## 代理检测设计

`ProxyDetector` 按优先级返回 `proxyUrl`：

```csharp
public sealed class ProxyDetector
{
    public string? Detect();
}
```

来源：
1. `NetStrataOptions.ProxyOverride`（来自 `NETSTRATA_PROXY`）
2. 环境变量
3. `WindowsSystemProxyReader`（注册表）
4. `WinHttpProxyReader`（netsh）
5. `LocalPortScanner`（可选 fallback）

---

## Daemon 设计

```csharp
public sealed class ProbeDaemon : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var sample = await _collector.CollectAsync(options, ct);
            await _storage.AppendSampleAsync(sample, ct);
            await _storage.WriteStateAsync(sample, ct);
            await Task.Delay(interval - elapsed, ct);
        }
    }
}
```

---

## 存储设计

```
%APPDATA%\NetStrata\
├── data\
│   ├── samples.jsonl      # 追加，每行一个 Sample JSON
│   └── state.json         # 覆盖写入
└── logs\
    └── daemon.log
```

```csharp
public interface ISampleStorage
{
    Task AppendAsync(Sample sample, CancellationToken ct);
    Task<IReadOnlyList<Sample>> ReadTailAsync(int limit, CancellationToken ct);
    Task WriteStateAsync(DaemonState state, CancellationToken ct);
    Task<DaemonState?> ReadStateAsync(CancellationToken ct);
}
```

---

## CLI 入口

```csharp
// NetStrata.Cli/Program.cs
var mode = args switch
{
    var a when a.Contains("--once") => RunMode.Once,
    var a when a.Contains("--web")  => RunMode.Web,
    _                               => RunMode.Tui,
};
```

使用 `System.CommandLine` 解析参数（可选）。

---

## 配置

```csharp
public sealed class NetStrataOptions
{
    public int IntervalMs { get; init; } = 60_000;
    public int Port { get; init; } = 8787;
    public string? ProxyOverride { get; init; }
    public string Lang { get; init; } = "auto";
    public int DownloadEvery { get; init; } = 10;
    public bool NoOpen { get; init; }
    public string DataDir { get; init; } = /* %APPDATA%/NetStrata/data */;
}
```

从环境变量 `NETSTRATA_*` 加载。

---

## 测试策略

遵循 Ponytail 约束：

| 测什么 | 怎么测 |
|--------|--------|
| `VerdictEngine` | 纯输入 Sample → 断言 verdict，无 I/O |
| `ProxyDetector` | mock 注册表 reader |
| `HttpsProbe` | mock `HttpMessageHandler`，断言 URL/Proxy |
| `PingProbe` | mock `IPingService` |
| 集成测试 | `[Trait("Category", "Integration")]`，默认跳过 |

**禁止**在单元测试中 `Process.Start` 真实浏览器或系统 exe。

---

## 发布

```powershell
dotnet publish src/NetStrata.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

输出：`netstrata.exe`（单文件，无运行时依赖）
