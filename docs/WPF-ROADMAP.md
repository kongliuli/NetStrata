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

---

## Phase W7 — 本地数据 + 图表/演示（✅ 已提交）

原则：**先稳住 `%APPDATA%\NetStrata\data`**，再叠 UI。网络流转与趋势都只读 `state.json` / 样本流，不另起数据源。

| 步 | ID | 状态 | 交付物 |
|----|-----|------|--------|
| 1 | W7a | ✅ | 日轮转 `samples-yyyyMMdd.jsonl` + 30 天 purge + 尾读 |
| 2 | W7b | ✅ | `trigger` + 手动/`--once` 入库 |
| 3 | W7c | ✅ | `judge` 可配置 + 周期预算 |
| 4 | W7d | ✅ | 历史趋势页（LiveCharts2） |
| 5 | W7e | ✅ | 网络流转演示（`FlowTrace` + `NetworkFlowControl`） |
| 6 | W7f | ✅ | 同窗整合 + README / 测试 |

---

## Phase W8 — 托盘常驻（下一版 · 执行计划）

### 目标

关主窗 ≠ 退出。进程与进程内 Daemon 以托盘为生命周期锚点；仅托盘「退出」停探测并 Shutdown。

### 现状缺口

- `App.OnStartup` 将 `ShutdownMode` 设为 `OnMainWindowClose`（覆盖 XAML 的 `OnExplicitShutdown`）
- `TrayHost.ShowMain` 只从 `Minimized` 恢复，关窗即进程没了
- 二次启动 Mutex 冲突只弹 MessageBox，不能激活已有窗

### 执行顺序（按 PR / 提交拆）

| 步 | ID | 改动面 | 做法 | 验收 |
|----|-----|--------|------|------|
| 1 | **W8a** | `App.xaml.cs`、`MainWindow` | GUI 保持 `OnExplicitShutdown`；`Closing`：若非 `_forceClose` 则 `e.Cancel=true` + `Hide()` | 点 × 后托盘仍在；约 5s 后 `state.json` 时间戳继续前进 |
| 2 | **W8b** | `TrayHost` | 「退出」：设 force-close → 停 Daemon → `main.Close()` → `Application.Shutdown()`；`OnExit` 已有 Dispose/Mutex 释放保持不变 | 任务管理器无残留 `NetStrata.exe` |
| 3 | **W8c** | `App.xaml.cs` + 薄 IPC | Mutex 抢失败时：`EventWaitHandle`（或等价）通知首实例 `ShowMain`，本进程 `Shutdown(0)`，去掉仅 MessageBox | 再开 exe → 已有主窗置前，无第二写盘进程 |
| 4 | **W8d** | `UserConfigLoader` / 设置窗 / `App` | `startMinimized`：启动时主窗不 `Show`（或 Show+Hide），只起托盘+Daemon | 登录自启不抢焦点；托盘双击可开窗 |
| 5 | **W8e** | `README.md`、`USAGE.md`、`UiStrings` | 文案「关闭窗口将继续在托盘运行」；设置项说明 | 与行为一致 |

### 实现要点（懒路径）

```text
W8a/b（一天内可完）:
  App: ShutdownMode = OnExplicitShutdown
  MainWindow.Closing += (s,e) => { if (!_forceClose) { e.Cancel=true; Hide(); } }
  TrayHost.Shutdown: _forceClose=true; StopDaemon; Close main; Application.Shutdown()

W8c（独立小步）:
  命名 EventWaitHandle "Local\\NetStrata.ShowMain"
  首实例后台 WaitOne → Dispatcher.Invoke(ShowMain)
  次实例 Set() 后退出

W8d（可跟设置窗一起）:
  config.json → startMinimized: bool
```

### 测试约束

- **禁止**单元测试里真启 WPF / `Process.Start`
- 可测：关闭策略纯函数 / `IMainWindowLifecycle` mock（Hide vs Shutdown 调用次数）
- 手工烟雾：关窗 → 托盘探测 → 再开 exe 激活 → 托盘退出

### 不做（YAGNI）

- 不拆独立 Daemon 子进程
- 不做 Windows 服务
- 不引 WebView2 / 新 UI 框架
- W8 不碰样本格式与趋势/流转数据路径
