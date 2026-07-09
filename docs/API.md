# Web API

NetStrata Web 模式在 `http://localhost:{PORT}` 提供本地 HTTP 服务。默认端口 **8787**（`NETSTRATA_PORT`）。

前端初期可移植 [canireach 的 index.html + chart.js](https://github.com/canireach/canireach/tree/main/public)，适配 `systemProxy` 字段差异。

---

## 端点一览

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/` | 仪表盘 HTML |
| GET | `/index.html` | 同上 |
| GET | `/chart.js` | Chart.js 库 |
| GET | `/favicon.svg` | 图标 |
| GET | `/api/state` | 最新 Daemon 状态 |
| GET | `/api/samples?limit=240` | 历史样本 |
| GET | `/api/series?limit=240` | 图表时序数据 |
| GET | `/api/conclusions` | Markdown 结论（Layer 3） |
| GET | `/api/export?minutes=60&format=json` | 导出报告（Layer 3） |

---

## GET /api/state

返回 `data/state.json` 内容。

**200**：

```json
{
  "startedAt": "2026-07-09T02:58:31.760Z",
  "cycle": 42,
  "latest": { /* Sample */ },
  "recentAlerts": [ /* Alert[], 最近 20 条 */ ],
  "rolling": { "last20Overall": { "ok": 15, "degraded": 5 } }
}
```

**503**：Daemon 尚未写入 state

```json
{ "error": "no state yet" }
```

---

## GET /api/samples

查询参数：

| 参数 | 默认 | 说明 |
|------|------|------|
| `limit` | 240 | 返回最近 N 条 |

**200**：

```json
{
  "count": 42,
  "samples": [ /* Sample[] */ ]
}
```

---

## GET /api/series

查询参数：

| 参数 | 默认 | 说明 |
|------|------|------|
| `limit` | 240 | 基于最近 N 条样本构建时序 |

**200**：见 [DATA-MODEL.md](DATA-MODEL.md#series图表时序数据)

---

## GET /api/conclusions

**200**：`text/markdown`

基于最近 N 条样本的规则引擎结论（`ConclusionEngine`）。Daemon 每 `NETSTRATA_CONCLUSION_EVERY` 轮（默认 30）更新 `data/conclusions.md`。规则见 [LAYER3.md](LAYER3.md#5-结论规则引擎phase-5c)。

无样本且无缓存时返回：

```markdown
_(no conclusions yet)_
```

---

## GET /api/export

查询参数：

| 参数 | 默认 | 说明 |
|------|------|------|
| `minutes` | 60 | 时间范围（分钟） |
| `format` | `json` | `json` 或 `markdown` |

**200**：诊断报告（overall 分布、各层统计、custom ping、recent alerts、conclusions 节选）。

CLI 等价：`netstrata --export --minutes 60 --format markdown -o report.md`

---

## 静态资源

放置于 `web/` 目录，通过 ASP.NET Core `UseStaticFiles` 或嵌入资源提供。

```
web/
├── index.html
├── chart.js
├── favicon.svg
└── avatar.svg
```

---

## CORS

本地服务，默认不需要 CORS。若前端开发分离，开发模式可加 `AllowAnyOrigin`。

---

## 实现示例（Minimal API）

```csharp
var app = WebApplication.CreateBuilder(args).Build();

app.UseStaticFiles();
app.MapGet("/api/state", async (ISampleStorage storage) =>
{
    var state = await storage.ReadStateAsync(CancellationToken.None);
    return state is null ? Results.Json(new { error = "no state yet" }, statusCode: 503) : Results.Json(state);
});
app.MapGet("/api/samples", async (int limit, ISampleStorage storage) =>
{
    var samples = await storage.ReadTailAsync(limit, CancellationToken.None);
    return Results.Json(new { count = samples.Count, samples });
});
app.MapGet("/api/series", async (int limit, ISeriesBuilder builder, ISampleStorage storage) =>
{
    var samples = await storage.ReadTailAsync(limit, CancellationToken.None);
    return Results.Json(builder.Build(samples));
});

app.Run($"http://localhost:{port}");
```

---

## 前端数据刷新

canireach 前端每 5 秒轮询 `/api/state` + `/api/series`。NetStrata 保持相同策略，后期可改 SSE/WebSocket。

---

## 安全说明

- 仅监听 `localhost`，不对外暴露
- 不存储敏感信息（SSID 可选脱敏 `ssidRedacted`）
- 代理 URL 不含凭据
