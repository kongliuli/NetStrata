# 测试规格

本文档定义 NetStrata **测试先行** 的验收标准。实现任何功能前，先在 `tests/` 中写失败测试，再写实现。

遵循 Ponytail 约束：**单元测试不得启动真实 GUI/系统进程**；集成测试标 `[Trait("Category", "Integration")]` 且默认跳过。

---

## 运行方式

```powershell
# 默认：仅单元测试
dotnet test

# 含集成测试（需真实网络）
dotnet test --filter "Category=Integration"
```

---

## Phase 1：核心探测 MVP

### VerdictEngineTests（必须先于实现）

| 测试方法 | 输入要点 | 断言 |
|----------|----------|------|
| `Judge_AllGreen_ReturnsHealthy` | 全层 ok | `overall == healthy` |
| `Judge_Ethernet_SkipsWifi` | `linkType=ethernet` | wifi.state == skipped |
| `Judge_GatewayPingFail_LanBad` | gw ping loss=100% | overall == lan_bad |
| `Judge_AliPingFail_BroadbandBad` | 223.5.5.5 fail | overall == broadband_bad |
| `Judge_OverseasFail_ProxyOk_DirectBlocked` | overseas fail + proxy ok | overall == direct_blocked_proxy_ok |
| `Judge_NoProxy_SkipsProxyLayer` | proxyUrl=null | proxy.state == skipped |
| `Judge_NoProxy_AiDirectOnly` | 两 AI 直连 ok | ai.state == direct_only |
| `Judge_ProxyNotListening_ProxyBad` | configured but !listening | proxy.state == fail |
| `Judge_BaiduSlow_Degraded` | baidu totalMs > 1500 | broadband.state == degraded |

### PingProbeTests（mock IPingService）

| 测试方法 | 断言 |
|----------|------|
| `Ping_ValidTarget_ReturnsStats` | ok, avgMs, lossPct 合理 |
| `Ping_Timeout_ReturnsNotOk` | ok=false, err 非空 |
| `Ping_CustomTarget_MarkedCustom` | `custom == true` 当目标来自 extra 列表 |

### HttpsProbeTests（mock HttpMessageHandler）

| 测试方法 | 断言 |
|----------|------|
| `Https_Direct_UsesNoProxy` | handler.UseProxy == false |
| `Https_Proxy_UsesWebProxy` | Proxy 指向指定 URL |
| `Https_AcceptAnyCode_403IsOk` | anthropic 403 → ok=true |
| `Https_200IsOk` | baidu 200 → ok=true |

### DnsProbeTests（mock IDnsQuery 或 stub）

| 测试方法 | 断言 |
|----------|------|
| `Dns_SystemServer_UsesDefault` | server == "system" |
| `Dns_NamedServer_QueriesCorrect` | 223.5.5.5 被传入 QueryServerAsync |
| `Dns_Matrix_Count` | 5×4 = 20 条结果 |

### SampleCollectorTests

| 测试方法 | 断言 |
|----------|------|
| `Collect_AttachesVerdict` | sample.verdict != null |
| `Collect_MergesExtraPingTargets` | pings 含内置 + extra |
| `Collect_Parallel_AllProbesCalled` | 各 mock probe 各调用一次 |

### CliIntegration（可选 Integration）

```powershell
netstrata --once | ConvertFrom-Json
# verdict.overall 非 null
# pings 含 223.5.5.5
```

---

## Phase 2：Windows 适配

### ProxyDetectorTests

| 测试方法 | 断言 |
|----------|------|
| `Detect_EnvOverride_Wins` | NETSTRATA_PROXY 优先 |
| `Detect_None_Disables` | `none` → null |
| `Detect_Registry_Fallback` | mock 注册表 → proxyUrl |
| `Detect_PortScan_Fallback` | 7890 listening → url |

### ProxyConfigProbeTests

| 测试方法 | 断言 |
|----------|------|
| `Probe_ListenerProcess_Resolved` | mock TCP + ProcessName |
| `Probe_SystemProxyPortMismatch_Degraded` | 注册表端口 ≠ 实际端口 |

### VerdictEngine Windows 扩展

| 测试方法 | 断言 |
|----------|------|
| `Judge_PingFail_HttpsOk_BroadbandDegraded` | ping fail + baidu ok → degraded 非 fail |

---

## Phase 3：Daemon + Web

### SampleStorageTests

| 测试方法 | 断言 |
|----------|------|
| `Append_WritesJsonlLine` | 每行合法 JSON |
| `ReadTail_ReturnsLastN` | limit 生效 |
| `WriteState_Overwrites` | 单文件覆盖 |

### SeriesBuilderTests

| 测试方法 | 断言 |
|----------|------|
| `Build_IncludesCustomPingSeries` | series.pings 含 custom_* 键 |
| `Build_AlignsTimestamps` | t[] 长度一致 |

### ApiTests（WebApplicationFactory）

| 测试方法 | 断言 |
|----------|------|
| `GetState_NoFile_Returns503` | 503 + error |
| `GetSamples_LimitQuery` | count ≤ limit |

---

## Phase 4：TUI + 发布

### 无 GUI 单元测试

TUI 逻辑抽离为 `TuiRenderer` / `StatusFormatter`，测字符串输出，不启动 Spectre Live。

---

## Layer 3（增强层）

见 [LAYER3.md](LAYER3.md)。测试摘要：

### TlsProbeTests

| 测试方法 | 断言 |
|----------|------|
| `Probe_DnsFail_StopsAtDns` | layer=dns, tcp/tls/http null |
| `Probe_TcpOk_TlsReset_TlsBlock` | TCP ok + TLS fail → tls_block |
| `Probe_FullStack_Ok` | 四层均 ok |

### RouteWatchTests

| 测试方法 | 断言 |
|----------|------|
| `Compare_GatewayChanged_EmitsAlert` | alerts 含 gateway_changed |
| `Compare_EgressIpChanged_EmitsAlert` | alerts 含 egress_changed |
| `Compare_NoChange_Empty` | alerts 空 |

### ConclusionEngineTests

| 测试方法 | 断言 |
|----------|------|
| `Generate_ProxyFlapping_MentionsUnstable` | Markdown 含「代理不稳定」 |
| `Generate_Healthy_Minimal` | 简短正面结论 |

### ExportTests

| 测试方法 | 断言 |
|----------|------|
| `Export_LastHour_IncludesSamples` | JSON/Markdown 非空 |
| `Export_CustomPings_Included` | 含 custom ping 行 |

---

## 自定义 Ping 专项

| 测试方法 | 断言 |
|----------|------|
| `Config_LoadsPingExtra_FromFile` | config.json pingExtra 合并 |
| `Config_EnvPingExtra_Overrides` | 环境变量与文件合并去重 |
| `Cli_PingFlag_MergesOnce` | `--ping 1.2.3.4` 仅当次生效 |
| `Validate_InvalidIp_Skipped` | 非法地址跳过并记 warn |
| `Validate_MaxTen_Enforced` | 最多 10 个 extra（ponytail 防滥用） |

---

## 测试数据：Sample Fixtures

`tests/NetStrata.Core.Tests/Fixtures/` 存放 JSON Sample 片段，供 VerdictEngine / SeriesBuilder 复用：

```
fixtures/
├── healthy.json
├── lan_bad.json
├── direct_blocked_proxy_ok.json
├── ping_blocked_https_ok.json
└── with_custom_pings.json
```

---

## CI 约定

```yaml
# .github/workflows/test.yml（规划）
- dotnet test --filter "Category!=Integration"
```

集成测试仅在维护者手动或 nightly 运行。
