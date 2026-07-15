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
| 1 | **W8a** | `App.xaml.cs`、`MainWindow` | ✅ GUI `OnExplicitShutdown`；`Closing`：非 `_forceClose` → Cancel+Hide | 点 × 后托盘仍在；`state.json` 继续更新 |
| 2 | **W8b** | `TrayHost` | ✅ 「退出」：停 Daemon → `AllowClose` → `Application.Shutdown` | 无残留进程 |
| 3 | **W8c** | `App.xaml.cs` + 薄 IPC | ✅ Mutex 抢失败时：`ShowMainSignal` 通知首实例 `ShowMain`，本进程退出 | 再开 exe → 已有主窗置前 |
| 4 | **W8d** | `UserConfigLoader` / 设置窗 / `App` | ✅ `startMinimized`：启动只留托盘+Daemon | 自启不抢焦点 |
| 5 | **W8e** | `README.md`、`USAGE.md`、设置说明 | ✅ 文案「关闭窗口将继续在托盘运行」 | 与行为一致 |

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

---

## Phase W9 — 探测链路可用性与信息架构（迭代方案）

原则：静态可读优先，动画只解释变化；一处事实源一处展示；异常永远置顶。

### 迭代 1 · 止血（W9a–c）

| 步 | 内容 | 验收 |
|---|---|---|
| W9a | ✅ 指纹跳过 Bind；播放中挂起播完再换 | 动画能完整播完 |
| W9b | ✅ `SetBlocks` 原地重排，不再 Clear 全部重建 | 5s 刷新无闪烁、展开态稳定 |
| W9c | ✅ 失败 > 降级 > 代理 > 正常；块头状态徽章 | 异常目标第一屏可见 |

### 迭代 2 · 信息架构（W9d–f）

| 步 | 内容 | 验收 |
|---|---|---|
| W9d | ✅ 展开即见静态终态路径，「播放」改为「重放」 | 不点播放也能看懂 |
| W9e | ✅ 出口切换标记：对比上一采样，块头显示「出口已切换 ⇄」 | 直连→代理切换一眼可见 |
| W9f | ✅ 顶部「公共路径」条，移除重复 ChainList 分层卡 | 一屏看清因果链 |

### 迭代 3 · 联动打磨（W9g–i）

| 步 | 内容 |
|---|---|
| W9g | ✅ 目标块「趋势」→ 跳转趋势页并显示关注横幅（分目标序列后续） |
| W9h | ✅ 出口/状态变化时块边框闪一下（事件通知，不自动播放） |
| W9i | ✅ 探测链路空状态可点「立即探测」（键盘可达不做） |

W8 收尾（W8c 二次启动激活已有窗、W8d startMinimized）与迭代 3 并行。

---

## Phase W10 — 通知告警可读页（进行中）

目标：告警持久化成独立页；中英白话；非专业用户也能读懂（不再直接甩 `proxy egress IPv4 → IPv6`）。

| 步 | ID | 状态 | 交付物 |
|----|-----|------|--------|
| 1 | W10a | ✅ | `AlertPresenter`：按 type 出标题+细节；缩短 IPv6 |
| 2 | W10b | ✅ | `alerts.jsonl` + Daemon 周期追加；主窗「通知告警」页 |
| 3 | W10c | ✅ | 总览条 / 托盘 Balloon / 导出报告改用可读文案 |
| 4 | W10d | ✅ | 空状态「立即探测」CTA；全部/重要/提醒/提示筛选；列表可展开详情 |
| 5 | W10e | ✅ | W8c 二次启动激活 + W9i 链路空状态探测；键盘可达 / W8d 另开 |

数据文件：`%APPDATA%\NetStrata\data\alerts.jsonl`（最多约 500 行）。
