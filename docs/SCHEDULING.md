# 调度与 Agent 集成

NetStrata **不提供独立 MCP Server**。外部系统（Cursor Agent、CI、任务计划、监控脚本）通过 **CLI**、**本地 HTTP API** 和 **数据文件** 进行调度与集成。

---

## 1. 调度模型总览

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│ 一次性 / 定时    │     │  常驻 Daemon      │     │  Agent / 自动化      │
│  netstrata      │     │  netstrata --web  │     │  loop / Task / CI   │
│  --once         │     │  → samples.jsonl  │     │  → CLI + API + 文件  │
│  --export       │     │  → state.json     │     │                     │
└────────┬────────┘     └────────┬─────────┘     └──────────┬──────────┘
         │                         │                          │
         └─────────────────────────┴──────────────────────────┘
                                   │
                    %APPDATA%\NetStrata\data\
```

| 场景 | 推荐方式 | 说明 |
|------|----------|------|
| 手动看一眼 | `netstrata` / `netstrata --once` | 无后台进程 |
| 持续监控 | `netstrata --web` | Daemon 写 jsonl + state |
| CI 门禁 | `netstrata --once` + 解析 JSON | 退出码 0，脚本判断 `verdict.overall` |
| 定时报告 | `netstrata --export` | 任务计划每日生成 report |
| Cursor 开发循环 | `/loop` + `scripts/agent-loop.ps1` | Agent 按 ROADMAP 持续推进 |
| Agent 读状态 | HTTP API 或读 `state.json` | 无需重复采集 |

---

## 2. CLI 式调度

### 2.1 一次性探测（脚本友好）

```powershell
$sample = netstrata --once | ConvertFrom-Json
$overall = $sample.verdict.overall
if ($overall -eq 'fail') { exit 1 }
exit 0
```

适合：登录脚本、VPN 切换后检查、部署后冒烟。

### 2.2 健康检查脚本

仓库提供 `scripts/health-check.ps1`（单元测试 + 可选 `--once`），供任务计划或 CI 调用：

```powershell
.\scripts\health-check.ps1              # 仅 dotnet test
.\scripts\health-check.ps1 -Probe       # 再加 netstrata --once
.\scripts\health-check.ps1 -Probe -FailOnDegraded  # degraded 也失败
```

### 2.3 Windows 任务计划

**常驻监控**（用户登录后启动 Web）：

```powershell
$exe = "C:\path\to\netstrata.exe"
$action = New-ScheduledTaskAction -Execute $exe -Argument "--web"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries
Register-ScheduledTask -TaskName "NetStrata-Web" -Action $action -Trigger $trigger -Settings $settings
```

**每日报告**：

```powershell
$report = "$env:USERPROFILE\Documents\netstrata-daily.md"
$action = New-ScheduledTaskAction -Execute $exe -Argument "--export --minutes 1440 -o `"$report`""
$trigger = New-ScheduledTaskTrigger -Daily -At 8am
Register-ScheduledTask -TaskName "NetStrata-DailyReport" -Action $action -Trigger $trigger
```

注意：`--export` 依赖 daemon 已写入的 `samples.jsonl`；仅 `--once` 不会产生历史序列。

### 2.4 退出码约定

| 命令 | 退出码 |
|------|--------|
| `netstrata --once` | 0（采集成功）；探测失败仍可能 0，需解析 JSON 中 `verdict` |
| `netstrata --web` | 进程常驻，Ctrl+C 结束 |
| `netstrata --export` | 0；无样本时报告为空但仍为 0 |
| 参数错误 | 1 |

脚本层应检查 `verdict.overall`，而非仅依赖进程退出码。

---

## 3. HTTP API 调度（Daemon 运行时）

`netstrata --web` 启动后，任意 HTTP 客户端（含 Agent 的 `curl`、`Invoke-RestMethod`、浏览器 MCP）可轮询：

```powershell
$base = "http://localhost:8787"

# 最新状态 + 告警
Invoke-RestMethod "$base/api/state"

# 时序图表数据
Invoke-RestMethod "$base/api/series?limit=60"

# Markdown 结论
Invoke-RestMethod "$base/api/conclusions"

# 导出 JSON 报告
Invoke-RestMethod "$base/api/export?minutes=60&format=json"
```

端点详见 [API.md](API.md)。

### 3.1 与浏览器 MCP 配合

Cursor 的 **cursor-ide-browser** MCP 可用于：

1. 打开 `http://localhost:8787` 目视确认仪表盘
2. 在自动化验收中截图告警横幅

**不用于**替代 `netstrata --once`：浏览器 MCP 适合 UI 验证，指标采集应走 CLI/API。

### 3.2 与 Shell MCP / Agent 配合

Agent 在本地仓库应优先：

```text
dotnet test
netstrata --once
netstrata --export --minutes 60 -o report.md
```

避免在单元测试中 `Process.Start` 弹窗（见项目 Ponytail 测试约束）。

---

## 4. 文件式集成（无 HTTP）

Daemon 或 `--once` 写入后，Agent 可直接读文件注入上下文：

| 文件 | 用途 |
|------|------|
| `data/state.json` | 最新样本 + `recentAlerts` + rolling |
| `data/samples.jsonl` | 历史序列（tail N 行） |
| `data/conclusions.md` | 人类可读结论 |
| `logs/daemon.log` | 错误与周期日志 |

```powershell
$root = "$env:APPDATA\NetStrata"
Get-Content "$root\data\state.json" -Raw
Get-Content "$root\data\samples.jsonl" -Tail 5
```

适合：离线分析、LLM 上下文、无端口环境。

---

## 5. Cursor Agent Loop（开发调度）

### 5.1 固定周期 `/loop`

在 Cursor 对话中使用（参见 Cursor Loop skill）：

```text
/loop 5m 继续 NetStrata ROADMAP：测试先行，Phase 完成则 commit+push
```

Agent 在后台 shell 中监听 `AGENT_LOOP_TICK_*` 哨兵，每轮执行 prompt。

### 5.2 仓库脚本 `scripts/agent-loop.ps1`

本仓库自带的 5 分钟 loop（PowerShell）：

```powershell
# 后台启动（Agent 应配置 notify_on_output 匹配 ^AGENT_LOOP_TICK_netstrata）
powershell -NoProfile -File scripts\agent-loop.ps1
```

每 300 秒输出：

```text
AGENT_LOOP_TICK_netstrata {"prompt":"NetStrata loop: (1) Continue..."}
```

Prompt 内嵌：按 ROADMAP 开发 → `dotnet test` 验收 → Phase 完成则 `git commit` + `git push origin main`。

停止：终止该 PowerShell 进程。

详见 [scripts/README.md](../scripts/README.md)。

### 5.3 Cursor Automations（可选）

若需 **云端/定时 Cursor Agent**（非本地 shell loop），使用 Cursor Automations 配置定时触发，instructions 中引用：

- 仓库路径与 `docs/ROADMAP.md`
- 验收命令：`dotnet test`、`netstrata --once`
- Git 推送规则（SSH 443，见用户 GitHub 规则）

Automations 与本地 `agent-loop.ps1` 二选一，避免重复调度。

---

## 6. MCP 定位说明

| 能力 | NetStrata 现状 | Agent 用法 |
|------|----------------|------------|
| **专用 MCP Server** | 未实现 | 不需要；CLI + HTTP 已覆盖 |
| **cursor-ide-browser** | 可选 | 打开仪表盘、UI 验收 |
| **cursor-app-control** | 可选 | 打开 `docs/`、`web/index.html` |
| **Shell 工具** | 推荐 | `dotnet test`、`netstrata *` |
| **HTTP fetch** | 推荐 | `/api/state` 等（daemon 运行时） |

### 6.1 推荐 Agent 工作流（每轮）

1. `git status` / `git log` — 确认分支与 Phase
2. `dotnet test` — 回归
3. 若改探测逻辑：`netstrata --once | ConvertFrom-Json` — 烟雾
4. 若改 Web/Daemon：`netstrata --web` + `Invoke-RestMethod /api/state`
5. Phase 完成：`git commit` + `git push origin main`
6. 更新 `docs/ROADMAP.md` 勾选项

### 6.2 未来：可选 NetStrata MCP Server（未实现）

若后续需要 IDE 内一键工具，可增加薄 MCP 包装，暴露例如：

| 工具名 | 行为 |
|--------|------|
| `netstrata_probe_once` | 等价 `--once`，返回 JSON |
| `netstrata_get_state` | 读 `state.json` |
| `netstrata_export` | 等价 `--export` |
| `netstrata_conclusions` | 读 conclusions 或调 `/api/conclusions` |

实现时可委托现有 `SampleCollector` / `ReportExporter`，**不重复**探测逻辑。当前阶段用 CLI 子进程即可。

---

## 7. CI 示例（GitHub Actions 片段）

```yaml
- name: Test
  run: dotnet test

# 可选：Windows runner 上烟雾探测（需网络）
- name: Probe once
  if: runner.os == 'Windows'
  run: dotnet run --project src/NetStrata.Cli -- --once
  continue-on-error: true
```

完整网络探测依赖真实网卡，CI 上主要保证 **编译 + 单元测试**；集成探测标 `[Trait("Category", "Integration")]` 默认跳过。

---

## 8. 相关文档

- [USAGE.md](USAGE.md) — 命令与配置
- [API.md](API.md) — HTTP 端点
- [TESTING.md](TESTING.md) — 测试与 Integration 约定
- [ROADMAP.md](ROADMAP.md) — Phase 验收
