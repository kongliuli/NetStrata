# 数据模型

本文档定义 NetStrata 核心 JSON 结构。实现时请用 C# record/class 一一对应，保持字段名 snake_case 或与 canireach 兼容的 camelCase（**统一用 camelCase**）。

---

## Sample（单次探测结果）

```json
{
  "t": "2026-07-09T03:01:32.076Z",
  "cycleMs": 98.6,
  "wifi": { /* WifiInfo */ },
  "iface": { /* InterfaceInfo */ },
  "tailscale": { /* TailscaleInfo */ },
  "dns": [ /* DnsResult[] */ ],
  "pings": [ /* PingResult[] */ ],
  "https": [ /* HttpsResult[] */ ],
  "proxyConfig": { /* ProxyConfig */ },
  "proxyEgress": { /* ProxyEgress | null */ },
  "captive": { /* CaptiveResult */ },
  "proxyDownload": { /* ProxyDownload | null */ },
  "tlsStack": [ /* TlsStackResult[] | null, Layer 3 */ ],
  "alerts": [ /* Alert[] | null, Layer 3 */ ],
  "verdict": { /* Verdict */ }
}
```

---

## WifiInfo

```json
{
  "status": "connected",
  "device": "Wi-Fi",
  "ssid": "MyNetwork",
  "ssidRedacted": false,
  "bssid": "AA:BB:CC:DD:EE:FF",
  "channel": 36,
  "band": "5",
  "rssi": -55,
  "noise": -90,
  "txRate": 866,
  "phyMode": "802.11ac",
  "security": "WPA2/WPA3",
  "countryCode": "CN"
}
```

`status` 枚举：`connected` | `disconnected` | `not_wifi` | `no_interface` | `unknown`

---

## InterfaceInfo

```json
{
  "primaryService": "Wi-Fi",
  "primaryDevice": "Wi-Fi",
  "hardwarePort": "Intel Wi-Fi 6",
  "linkType": "wifi",
  "ipv4": "192.168.1.100",
  "gateway": "192.168.1.1",
  "subnetMask": "255.255.255.0",
  "dhcpServer": "192.168.1.1",
  "dhcpDns": ["192.168.1.1"]
}
```

`linkType` 枚举：`wifi` | `ethernet` | `other` | `unknown`

---

## PingResult

```json
{
  "target": "223.5.5.5",
  "label": null,
  "custom": false,
  "ok": true,
  "lossPct": 0,
  "sent": 3,
  "received": 3,
  "minMs": 4.2,
  "avgMs": 5.1,
  "maxMs": 6.0,
  "stddevMs": 0.8,
  "err": null
}
```

自定义目标示例：

```json
{
  "target": "192.168.1.50",
  "label": "nas",
  "custom": true,
  "ok": true,
  "lossPct": 0,
  "sent": 3,
  "received": 3,
  "minMs": 1.0,
  "avgMs": 1.2,
  "maxMs": 1.5,
  "stddevMs": 0.2,
  "err": null
}
```

---

## DnsResult

```json
{
  "server": "223.5.5.5",
  "domain": "baidu.com",
  "ok": true,
  "ms": 12.5,
  "ips": ["110.242.74.102", "111.63.65.247"],
  "flags": "NOERROR",
  "err": null
}
```

`server` 为 `"system"` 时表示使用系统默认 DNS。

---

## HttpsResult

```json
{
  "label": "baidu_direct",
  "url": "https://www.baidu.com",
  "via": "direct",
  "ok": true,
  "httpCode": 200,
  "remoteIp": "110.242.74.102",
  "dnsMs": 12.0,
  "connectMs": 45.0,
  "tlsMs": 80.0,
  "firstByteMs": 120.0,
  "totalMs": 350.0,
  "timedOut": false,
  "err": null
}
```

---

## ProxyConfig

```json
{
  "proxyUrl": "http://127.0.0.1:7890",
  "proxyPort": 7890,
  "envHttp": null,
  "envHttps": null,
  "envAll": null,
  "systemProxy": {
    "httpEnable": true,
    "httpProxy": "127.0.0.1",
    "httpPort": 7890,
    "httpsEnable": true,
    "httpsProxy": "127.0.0.1",
    "httpsPort": 7890,
    "socksEnable": false,
    "socksProxy": null,
    "socksPort": null,
    "autoDetect": false,
    "bypassList": "<local>"
  },
  "listening": true,
  "listenerProcess": "verge-mihomo"
}
```

> **与 canireach 差异**：`scutil` 字段在 Windows 版替换为 `systemProxy`（注册表读取）。前端适配时需处理此差异。

---

## ProxyEgress

```json
{
  "ok": true,
  "ip": "1.2.3.4",
  "ms": 800.0,
  "err": null
}
```

---

## CaptiveResult

```json
{
  "ok": true,
  "httpCode": 200,
  "bodyHead": "Success",
  "redirected": false,
  "totalMs": 150.0
}
```

---

## ProxyDownload

```json
{
  "ok": true,
  "bytes": 5000000,
  "ms": 3200.0,
  "mbps": 12.5,
  "err": null
}
```

---

## TlsStackResult（Layer 3）

```json
{
  "label": "stack_github",
  "host": "github.com",
  "port": 443,
  "dns": { "ok": true, "ms": 15.0, "ips": ["140.82.121.4"], "err": null },
  "tcp": { "ok": true, "ms": 45.0, "err": null },
  "tls": { "ok": true, "ms": 120.0, "err": null },
  "http": { "ok": true, "ms": 200.0, "httpCode": 200, "err": null },
  "verdict": "ok",
  "stoppedAt": null
}
```

`verdict` 枚举：`ok` | `dns_fail` | `tcp_fail` | `tls_block` | `http_block` | `unknown`

`stoppedAt`：首层失败时记录 `dns` | `tcp` | `tls` | `http`。

---

## Alert（Layer 3）

```json
{
  "t": "2026-07-09T04:00:00Z",
  "type": "egress_changed",
  "message": "proxy egress 1.2.3.4 → 5.6.7.8",
  "prev": "1.2.3.4",
  "curr": "5.6.7.8"
}
```

`type` 枚举：`gateway_changed` | `ipv4_changed` | `egress_changed` | `interface_changed` | `proxy_down` | `egress_flapping` | `tls_block`

---

## UserConfig（`config.json`）

```json
{
  "pingExtra": ["192.168.1.50", "10.0.0.1"],
  "pingExtraLabels": {
    "192.168.1.50": "nas"
  },
  "tlsStackTargets": ["github.com", "api.anthropic.com"]
}
```

路径：`%APPDATA%\NetStrata\config.json`。不存在时使用内置默认。

---

## Verdict（判决）

```json
{
  "overall": "healthy",
  "headline": "all green",
  "insights": [],
  "layers": [
    {
      "layer": "wifi",
      "state": "ok",
      "reasons": [],
      "metrics": { "rssi": -55, "txRate": 866, "linkType": "wifi" }
    },
    {
      "layer": "lan",
      "state": "ok",
      "reasons": [],
      "metrics": { "gateway": "192.168.1.1", "avgMs": 2.1, "loss": 0 }
    },
    {
      "layer": "broadband",
      "state": "ok",
      "reasons": [],
      "metrics": {}
    },
    {
      "layer": "overseas_direct",
      "state": "fail",
      "reasons": ["all direct overseas fail (blocked)"],
      "metrics": {}
    },
    {
      "layer": "proxy",
      "state": "ok",
      "reasons": [],
      "metrics": {
        "configured": true,
        "proxyUrl": "http://127.0.0.1:7890",
        "listening": true,
        "egressIp": "1.2.3.4",
        "listenerProcess": "verge-mihomo"
      }
    },
    {
      "layer": "ai",
      "state": "ok",
      "reasons": [],
      "metrics": {
        "anthropicProxyOk": true,
        "openaiProxyOk": true,
        "anthropicDirectOk": false,
        "openaiDirectOk": false
      }
    }
  ],
  "ai": {
    "state": "proxy_only",
    "headline": "Anthropic & OpenAI reachable via proxy; direct blocked"
  }
}
```

### layer 枚举

`wifi` | `lan` | `broadband` | `overseas_direct` | `proxy` | `ai`

### state 枚举

`ok` | `degraded` | `fail` | `skipped` | `unknown`

### overall 枚举

`healthy` | `degraded` | `wifi_bad` | `lan_bad` | `broadband_bad` | `proxy_bad` | `direct_blocked_proxy_ok` | `unknown`

### ai.state 枚举

`ok` | `proxy_only` | `direct_only` | `degraded` | `fail` | `skipped` | `unknown`

---

## State（Daemon 快照）

`data/state.json` 结构：

```json
{
  "startedAt": "2026-07-09T02:58:31.760Z",
  "cycle": 42,
  "latest": { /* Sample */ },
  "recentAlerts": [ /* Alert[], 最近 20 条 */ ],
  "rolling": {
    "last20Overall": { "healthy": 18, "degraded": 2 },
    "httpsAgg": {
      "baidu_direct": { "okRate": 1.0, "avgMs": 320 }
    }
  }
}
```

---

## Series（图表时序数据）

`GET /api/series` 返回结构（供 Chart.js 消费）：

```json
{
  "t": ["2026-07-09T03:00:00Z", "..."],
  "verdict": ["healthy", "..."],
  "wifi": {
    "rssi": [-55, null],
    "txRate": [866, null],
    "noise": [-90, null],
    "channel": [36, null],
    "ssid": ["MyNetwork", null]
  },
  "pings": {
    "gw": [2.1, null],
    "ali": [5.1, 5.3],
    "cf": [180, null],
    "goo": [200, null]
  },
  "https": {
    "baidu_direct": {
      "totalMs": [350, 400],
      "ok": [true, true],
      "timedOut": [false, false]
    }
  },
  "dns": {
    "223.5.5.5": [12.5, 15.0]
  },
  "proxy": {
    "egressIp": ["1.2.3.4", "1.2.3.4"],
    "egressMs": [800, 750],
    "listening": [true, true],
    "downloadMbps": [12.5, null]
  },
  "captive": [true, true],
  "layers": {
    "wifi": ["ok", "skipped"],
    "lan": ["ok", "ok"],
    "broadband": ["ok", "ok"],
    "overseas_direct": ["fail", "fail"],
    "proxy": ["ok", "ok"],
    "ai": ["ok", "ok"]
  },
  "ai": {
    "state": ["proxy_only", "proxy_only"],
    "anthropicProxy": [true, true],
    "anthropicDirect": [false, false],
    "openaiProxy": [true, true],
    "openaiDirect": [false, false]
  }
}
```

---

## C# 类型命名建议

```
NetStrata.Core.Models/
├── Sample.cs
├── WifiInfo.cs
├── InterfaceInfo.cs
├── PingResult.cs
├── TlsStackResult.cs
├── Alert.cs
├── UserConfig.cs
├── DnsResult.cs
├── HttpsResult.cs
├── ProxyConfig.cs
├── SystemProxySettings.cs
├── ProxyEgress.cs
├── CaptiveResult.cs
├── ProxyDownload.cs
├── Verdict.cs
├── LayerVerdict.cs
├── AiVerdict.cs
└── DaemonState.cs
```

JSON 序列化：`System.Text.Json`，属性名 camelCase：

```csharp
[JsonPropertyName("cycleMs")]
public double CycleMs { get; init; }
```
