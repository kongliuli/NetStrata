# Scripts

## agent-loop.ps1

**用途**：Cursor Agent 开发循环的 5 分钟固定 tick（PowerShell）。

```powershell
powershell -NoProfile -File scripts\agent-loop.ps1
```

- 每 **300 秒**输出一行：`AGENT_LOOP_TICK_netstrata {"prompt":"..."}`
- Agent 应在 Shell 上配置 `notify_on_output`，正则：`^AGENT_LOOP_TICK_netstrata`
- Prompt 内容：按 ROADMAP 开发 → `dotnet test` → Phase 完成则 commit + push

**停止**：结束该 PowerShell 进程（任务管理器或 `Stop-Process`）。

与 Cursor 对话命令 `/loop 5m ...` 等效；本脚本便于 Windows 下无需手打 loop 语法。

## agent-loop-wpf.ps1

**用途**：WPF Phase W6 动态循环（每 5 分钟 wake）。

```powershell
powershell -NoProfile -File scripts\agent-loop-wpf.ps1
```

- 输出：`AGENT_LOOP_WAKE_netstrata_wpf {"prompt":"..."}`
- Agent 正则：`^AGENT_LOOP_WAKE_netstrata_wpf`
- 按 [docs/WPF-ROADMAP.md](../docs/WPF-ROADMAP.md) 推进 W6b→W6h

## publish.ps1

**用途**：发布 `netstrata.exe` + `netstrata-tray.exe` 到 `artifacts/publish`（win-x64 单文件）。

```powershell
.\scripts\publish.ps1
```



## health-check.ps1

**用途**：CLI 式健康检查，供任务计划或 CI 调用。

```powershell
.\scripts\health-check.ps1
.\scripts\health-check.ps1 -Probe
.\scripts\health-check.ps1 -Probe -FailOnDegraded
```

| 参数 | 说明 |
|------|------|
| （无） | 仅 `dotnet test` |
| `-Probe` | 额外运行 `netstrata --once` |
| `-FailOnDegraded` | `verdict.overall` 为 `degraded` 或 `fail` 时退出码 1 |

详见 [docs/SCHEDULING.md](../docs/SCHEDULING.md)。
