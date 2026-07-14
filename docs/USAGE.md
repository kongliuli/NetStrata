# 使用指南

本文档描述 NetStrata 的安装、运行模式、配置与数据目录。调度与 Agent 集成见 [SCHEDULING.md](SCHEDULING.md)。

---

## 1. 安装与构建

### 开发构建

```powershell
cd D:\FromGit\NetStrata
dotnet build
dotnet test
dotnet run --project src/NetStrata.Tray -- --help
```

### 单文件发布（推荐分发）

```powershell
.\scripts\publish.ps1

# 产出 NetStrata.exe（WPF 托盘 + CLI）
.\artifacts\publish\NetStrata.exe --help
```

可将 `NetStrata.exe` 加入 `PATH`，或在发布目录直接运行。

---

## 2. 运行模式

| 模式 | 命令 | 说明 |
|------|------|------|
| **托盘（默认）** | `NetStrata` | WPF 主窗 + 托盘 + **进程内 Daemon**；设置 / 告警气泡 |
| **TUI** | `NetStrata --tui` | Spectre.Console 实时面板；`q` 退出、`l` 语言、`r` 刷新 |
| **TUI 只读** | `NetStrata --follow` | 仅读 daemon 的 `state.json`，不主动采集 |
| **单次探测** | `NetStrata --once` | 采集一轮，JSON 输出到 stdout（适合脚本/CI） |
| **导出报告** | `NetStrata --export -o report.md` | 基于历史样本生成 Markdown/JSON 报告 |
| **帮助** | `NetStrata --help` | 打印用法与环境变量摘要 |

> `--web` 本阶段未启用。探测由托盘进程内 Daemon 完成；Web 仪表盘后续再做。

### 2.1 单次探测 + 自定义 Ping

```powershell
NetStrata --once --ping 192.168.1.50,10.0.0.1
```

自定义目标合并进 `sample.pings[]`（`custom: true`），**不参与** 6 层 verdict，仅供观测与导出。

### 2.2 托盘 UI

```powershell
dotnet run --project src/NetStrata.Tray
# 或
.\artifacts\publish\NetStrata.exe
```

启动后显示主窗口并开始进程内探测。主窗 Tab：

| Tab | 内容 |
|-----|------|
| **总览** | 分层摘要、全部 AI 卡片、自定义目标、本机网络 |
| **探测链路** | 各层判决原因与指标（中文） |
| **AI / API** | 6 家 API 可达性；单击/右键打开**官网**（探测走 API 端点） |
| **自定义目标** | 页内添加 Ping 主机或 `https://` URL（写入 `pingExtra` / `httpsExtra` 并热重载） |
| **本机网络** | IPv4 / 网关 / DNS / Wi‑Fi / 代理 / 出口 IP / Tailscale |

托盘菜单：启停 Daemon、立即探测、打开主窗、设置。外观主题（跟随系统 / 浅色 / 深色）由 HandyControl 皮肤 + 自有色板同步。

### 2.3 导出报告

```powershell
NetStrata --export --minutes 60 -o report.md
NetStrata --export --minutes 120 --format json -o report.json
NetStrata --export --minutes 30 | Out-File -Encoding utf8 report.md
```

### 2.4 TUI

```powershell
NetStrata --tui
NetStrata --follow
$env:NETSTRATA_LANG = 'zh'
NetStrata --tui
```

---

## 3. 配置文件

路径：`%APPDATA%\NetStrata\config.json`（见 [config.example.json](config.example.json)）。

| 字段 | 说明 |
|------|------|
| `intervalMs` | 探测间隔 |
| `port` | 预留（Web 后续） |
| `lang` | 界面语言 `zh` / `en` / `auto`（默认中文） |
| `theme` | `system` / `light` / `dark` |
| `pingExtra` | 额外 ping 目标（最多 10 个；主窗「自定义目标」页编辑） |
| `pingExtraLabels` | 目标 → 显示名 |
| `httpsExtra` | 额外 HTTPS 探测 URL（自定义目标页添加） |
| `tlsStackTargets` | 覆盖 TLS 栈探测主机 |

设置窗口保存语言/主题/间隔/TLS/开机启动后会**热重载**进程内 Daemon；自定义目标在主窗 Targets Tab 维护，不会被设置页清空。

优先级（高 → 低）：CLI `--ping` → `NETSTRATA_PING_EXTRA` → `config.json`。

---

## 4. 环境变量

| 变量 | 默认 | 说明 |
|------|------|------|
| `NETSTRATA_INTERVAL_MS` | `60000` | Daemon 探测间隔（毫秒） |
| `NETSTRATA_PORT` | `8787` | 预留（Web 后续） |
| `NETSTRATA_PROXY` | auto | 强制代理 URL；`none`/`off` 禁用 |
| `NETSTRATA_LANG` | `auto` | `zh` / `en`（TUI） |
| `NETSTRATA_DOWNLOAD_EVERY` | `10` | 每 N 轮测一次代理下载速度 |
| `NETSTRATA_CONCLUSION_EVERY` | `30` | 每 N 轮重写 `conclusions.md` |
| `NETSTRATA_PING_EXTRA` | — | 逗号分隔额外 ping，最多 10 个 |

---

## 5. 数据目录

根目录：`%APPDATA%\NetStrata\`

```
%APPDATA%\NetStrata\
├── config.json
├── data\
│   ├── samples.jsonl
│   ├── state.json
│   └── conclusions.md
└── logs\
    └── daemon.log
```

### 5.1 快速检查

```powershell
Get-Content "$env:APPDATA\NetStrata\data\state.json" | ConvertFrom-Json |
  Select-Object -ExpandProperty latest |
  Select-Object -ExpandProperty verdict

Get-Content "$env:APPDATA\NetStrata\logs\daemon.log" -Tail 20
```

---

## 6. 验收命令（冒烟）

```powershell
dotnet test --filter "Category!=Integration"

NetStrata --once | ConvertFrom-Json | Select-Object -ExpandProperty verdict

dotnet run --project src/NetStrata.Tray
# 托盘图标应变色；打开 Dashboard 应约 5s 刷新

NetStrata --export --minutes 60 -o $env:TEMP\netstrata-report.md
```

---

## 7. 相关文档

| 文档 | 内容 |
|------|------|
| [SCHEDULING.md](SCHEDULING.md) | CLI / 任务计划 / Cursor Loop |
| [SPEC.md](SPEC.md) | 探测与判决规格 |
| [ARCHITECTURE.md](ARCHITECTURE.md) | 解决方案结构 |
| [WPF-ROADMAP.md](WPF-ROADMAP.md) | WPF 与单 exe 架构 |
