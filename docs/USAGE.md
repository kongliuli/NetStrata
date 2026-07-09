# 使用指南

本文档描述 NetStrata 的安装、运行模式、配置与数据目录。调度与 Agent 集成见 [SCHEDULING.md](SCHEDULING.md)。

---

## 1. 安装与构建

### 开发构建

```powershell
cd C:\Users\yf\Projects\NetStrata
dotnet build
dotnet test
dotnet run --project src/NetStrata.Cli/NetStrata.Cli.csproj -- --help
```

### 单文件发布（推荐分发）

```powershell
dotnet publish src/NetStrata.Cli/NetStrata.Cli.csproj -c Release -r win-x64 `
  -o .\artifacts\publish

# 产出 netstrata.exe（PublishTrimmed 后约 19MB，自包含运行时）
.\artifacts\publish\netstrata.exe --help
```

可将 `netstrata.exe` 加入 `PATH`，或在发布目录直接运行。

---

## 2. 运行模式

| 模式 | 命令 | 说明 |
|------|------|------|
| **TUI（默认）** | `netstrata` | Spectre.Console 实时面板，6 层 + AI；快捷键 `q` 退出、`l` 语言、`r` 刷新 |
| **TUI 只读** | `netstrata --follow` | 仅读 daemon 的 `state.json`，不主动采集 |
| **单次探测** | `netstrata --once` | 采集一轮，JSON 输出到 stdout（适合脚本/CI） |
| **Web + Daemon** | `netstrata --web` | 后台探测循环 + `http://localhost:8787` 仪表盘 |
| **导出报告** | `netstrata --export -o report.md` | 基于历史样本生成 Markdown/JSON 报告 |
| **帮助** | `netstrata --help` | 打印用法与环境变量摘要 |

### 2.1 单次探测 + 自定义 Ping

```powershell
netstrata --once --ping 192.168.1.50,10.0.0.1
```

自定义目标合并进 `sample.pings[]`（`custom: true`），**不参与** 6 层 verdict，仅供观测与导出。

### 2.2 Web 仪表盘

```powershell
$env:NETSTRATA_NO_OPEN = '1'          # 不自动打开浏览器
$env:NETSTRATA_INTERVAL_MS = '20000'  # 20 秒一轮
netstrata --web
# 浏览器访问 http://localhost:8787
```

API 详情见 [API.md](API.md)。

### 2.3 导出报告

```powershell
# Markdown（默认）
netstrata --export --minutes 60 -o report.md

# JSON
netstrata --export --minutes 120 --format json -o report.json

# 输出到 stdout（管道/重定向）
netstrata --export --minutes 30 | Out-File -Encoding utf8 report.md
```

报告含：时间范围、overall 分布、各层统计、自定义 ping 摘要、近期告警、结论节选。

### 2.4 TUI

```powershell
netstrata                    # 默认：有 state 则读 state，否则现场采集
netstrata --follow           # 只读 daemon，等待 state 出现
$env:NETSTRATA_LANG = 'zh'   # 或 TUI 内按 l 切换
netstrata
```

---

## 3. 配置文件

路径：`%APPDATA%\NetStrata\config.json`（见 [config.example.json](config.example.json)）。

| 字段 | 说明 |
|------|------|
| `pingExtra` | 额外 ping 目标（最多 10 个） |
| `pingExtraLabels` | 目标 → 显示名，如 `"192.168.1.50": "nas"` |
| `tlsStackTargets` | 覆盖 TLS 栈探测主机，如 `["github.com", "api.anthropic.com:443"]` |

优先级（高 → 低）：

1. CLI 单次：`--ping`
2. 环境变量：`NETSTRATA_PING_EXTRA`
3. `config.json`

---

## 4. 环境变量

| 变量 | 默认 | 说明 |
|------|------|------|
| `NETSTRATA_INTERVAL_MS` | `60000` | Daemon 探测间隔（毫秒） |
| `NETSTRATA_PORT` | `8787` | Web 监听端口 |
| `NETSTRATA_PROXY` | auto | 强制代理 URL；`none`/`off` 禁用自动检测 |
| `NETSTRATA_LANG` | `auto` | `zh` / `en`（TUI） |
| `NETSTRATA_DOWNLOAD_EVERY` | `10` | 每 N 轮测一次代理下载速度 |
| `NETSTRATA_CONCLUSION_EVERY` | `30` | 每 N 轮重写 `conclusions.md` |
| `NETSTRATA_NO_OPEN` | `0` | `1` = `--web` 时不打开浏览器 |
| `NETSTRATA_PING_EXTRA` | — | 逗号分隔额外 ping，最多 10 个 |

---

## 5. 数据目录

根目录：`%APPDATA%\NetStrata\`

```
%APPDATA%\NetStrata\
├── config.json           # 用户配置
├── data\
│   ├── samples.jsonl     # 历史样本（每行一个 Sample JSON）
│   ├── state.json        # Daemon 最新状态 + recentAlerts
│   └── conclusions.md    # 周期性 Markdown 结论
└── logs\
    └── daemon.log        # Daemon 文本日志
```

### 5.1 快速检查

```powershell
# 最新判决
Get-Content "$env:APPDATA\NetStrata\data\state.json" | ConvertFrom-Json |
  Select-Object -ExpandProperty latest |
  Select-Object -ExpandProperty verdict

# 最近告警
(Get-Content "$env:APPDATA\NetStrata\data\state.json" | ConvertFrom-Json).recentAlerts

# Daemon 日志尾部
Get-Content "$env:APPDATA\NetStrata\logs\daemon.log" -Tail 20
```

---

## 6. 验收命令（冒烟）

```powershell
dotnet test

# 单次 JSON（应含 verdict.overall）
netstrata --once | ConvertFrom-Json | Select-Object -ExpandProperty verdict

# Web（另开终端，需先 build/run）
$env:NETSTRATA_NO_OPEN = '1'
$env:NETSTRATA_INTERVAL_MS = '20000'
netstrata --web
# Invoke-RestMethod http://localhost:8787/api/state
# Invoke-RestMethod http://localhost:8787/api/conclusions

# 导出
netstrata --export --minutes 60 -o $env:TEMP\netstrata-report.md
```

---

## 7. 相关文档

| 文档 | 内容 |
|------|------|
| [SCHEDULING.md](SCHEDULING.md) | CLI / 任务计划 / Cursor Loop / MCP 与 HTTP 调度 |
| [API.md](API.md) | Web HTTP 端点 |
| [SPEC.md](SPEC.md) | 探测与判决规格 |
| [LAYER3.md](LAYER3.md) | TLS 栈、告警、导出 |
| [ROADMAP.md](ROADMAP.md) | 分期计划（当前 Phase 0–5 已完成） |
