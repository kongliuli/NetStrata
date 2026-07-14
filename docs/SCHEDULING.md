# 调度与 Agent 集成

NetStrata **不提供独立 MCP Server**。外部系统通过 **同一 `NetStrata.exe` 的 CLI 子命令** 和 **数据文件** 调度。Web HTTP API 本阶段未启用。

---

## 1. 调度模型总览

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│ 一次性 / 定时    │     │  常驻（托盘）      │     │  Agent / 自动化      │
│  NetStrata      │     │  NetStrata.exe    │     │  loop / Task / CI   │
│  --once         │     │  进程内 Daemon    │     │  → CLI + 文件        │
│  --export       │     │  → state.json     │     │                     │
└────────┬────────┘     └────────┬─────────┘     └──────────┬──────────┘
         │                         │                          │
         └─────────────────────────┴──────────────────────────┘
                                   │
                    %APPDATA%\NetStrata\data\
```

| 场景 | 推荐方式 | 说明 |
|------|----------|------|
| 手动看一眼 | 托盘 / `NetStrata --once` | 托盘默认常驻探测 |
| 持续监控 | `NetStrata.exe`（无参） | 进程内 Daemon 写 jsonl + state |
| CI 门禁 | `NetStrata --once` + 解析 JSON | 退出码 0，脚本判断 `verdict.overall` |
| 定时报告 | `NetStrata --export` | 任务计划每日生成 report |
| Cursor 开发循环 | `/loop` + `scripts/agent-loop.ps1` | Agent 按 ROADMAP 持续推进 |
| Agent 读状态 | 读 `state.json` | 无需重复采集 |

---

## 2. CLI 式调度

### 2.1 一次性探测

```powershell
$sample = & .\artifacts\publish\NetStrata.exe --once | ConvertFrom-Json
$overall = $sample.verdict.overall
if ($overall -eq 'fail') { exit 1 }
```

### 2.2 导出

```powershell
& .\artifacts\publish\NetStrata.exe --export --minutes 60 -o report.md
```

### 2.3 任务计划（开机托盘）

```powershell
$exe = "C:\path\to\NetStrata.exe"
$action = New-ScheduledTaskAction -Execute $exe
# 或仅探测：-Argument "--once"
```

也可在设置页勾选「登录时启动」。

---

## 3. 数据文件

| 路径 | 用途 |
|------|------|
| `%APPDATA%\NetStrata\data\state.json` | 最新 Sample + recentAlerts |
| `%APPDATA%\NetStrata\data\samples.jsonl` | 历史样本 |
| `%APPDATA%\NetStrata\data\conclusions.md` | 周期性结论 |
| `%APPDATA%\NetStrata\logs\daemon.log` | Daemon 日志 |

---

## 4. 健康检查脚本

```powershell
.\scripts\health-check.ps1
.\scripts\health-check.ps1 -Probe
.\scripts\health-check.ps1 -Probe -FailOnDegraded
```

---

## 5. 相关文档

- [USAGE.md](USAGE.md) — 安装与命令
- [WPF-ROADMAP.md](WPF-ROADMAP.md) — 单 exe 架构
