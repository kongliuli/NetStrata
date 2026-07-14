# NetStrata.Tray — WPF 与单 exe

> **主入口**：`NetStrata.exe`（原 Tray 升格）。无参启动托盘 + 进程内 Daemon；`--once` / `--export` / `--tui` 等为同 exe CLI。Web 仪表盘本阶段不做。

## 原则

1. **每步可单独合并** — 跑通 `dotnet build` + 相关测试
2. **不是薄壳** — UI、状态机、Daemon 生命周期在 Tray 内；Core 放可复用逻辑
3. **单元测试不启动 GUI** — `InProcessDaemonController`、`OnceProbeRunner`、`TrayStatusMapper` 等在 Core 测；禁止真 `Process.Start`

---

## 项目结构

```
src/
  NetStrata.Core/       # 探测、判决、存储、进程内 Daemon 控制
  NetStrata.Daemon/     # ProbeDaemon 循环
  NetStrata.Tray/       # NetStrata.exe（WPF + CLI 分发）
```

---

## Phase W6（历史）— 已完成

| 步 | ID | 状态 | 交付物 |
|----|-----|------|--------|
| 1–8 | W6a–W6h | ✅ | 托盘、Dashboard、设置、告警、自启、发布 |

---

## 单 exe 重构 — 已完成

| 项 | 说明 |
|----|------|
| 进程内 Daemon | `InProcessDaemonController` + `ProbeDaemon.ProbeLoopAsync`；启动即探测 |
| 进程内 --once | `OnceProbeRunner` 调 `SampleCollector`，不 spawn 外部进程 |
| 单入口 | `App` → CLI 参数走 `CommandDispatcher`（AttachConsole）；无参起托盘 |
| 发布 | `scripts/publish.ps1` → `artifacts/publish/NetStrata.exe` |
| Web | `--web` 返回未启用提示；后续再做 |

### 验收

```powershell
dotnet test --filter "Category!=Integration"
dotnet run --project src/NetStrata.Tray -- --help
dotnet run --project src/NetStrata.Tray -- --once
dotnet run --project src/NetStrata.Tray
# 托盘应自动探测；Dashboard 约 5s 刷新；设置保存应热重载 Daemon
.\scripts\publish.ps1
.\artifacts\publish\NetStrata.exe --help
```
