# UI 业务流梳理与 HandyControl 深度美化方案（Phase W11）

> 编写模型: Claude Fable 5 (Cursor Agent) · 2026-07-16

> 定位：spec 类文档。梳理当前 WPF 页面业务流的不顺点，并给出「深用 HandyControl（已装 3.5.1）」的美化与交互修复方案。全部实现并验证后整篇归档。
> 姊妹文档：[../DESIGN.md](../DESIGN.md)（设计系统现状）、[WPF-ROADMAP.md](WPF-ROADMAP.md)（W8–W10 历史）。

---

## 1. 现状速览

- 主窗 = `hc:Window` + 左侧 `hc:SideMenu`（208px）+ 右侧 **7 个 ScrollViewer 叠显**（`Visibility` 切换），无独立 View/ViewModel 文件，逻辑集中在 `MainWindow.xaml.cs`（约 1000 行）。
- 数据管道：Daemon/手动探测 → `state.json` → 主窗与托盘 **各自 5s 轮询** → Core 的 `*Mapper` → code-behind 填充控件。无事件推送。
- HandyControl 使用面：`hc:Window` / `SideMenu` / `Card` / `Tag` / `UniformSpacingPanel` / `BorderElement` / `InfoElement`，**约占其能力的 10%**；链路动画、趋势图、托盘（WinForms `NotifyIcon`）均在 HC 之外。
- 主题：HC Skin（Default/Dark）+ `ThemeApplier` 注入少量自有键（`CardBg` 等）；但大量语义色在 XAML/C# 里硬编码 hex。

---

## 2. 业务流不顺点（按影响排序）

### A. 主任务断链 —「哪一层坏了」看得见、点不动

产品的主线任务是「总览发现异常 → 下钻到链路看断点」。目前每一步都靠用户自己去侧栏找：

| # | 问题 | 证据 |
|---|------|------|
| A1 | 总览分层状态条**不可点击**，无法下钻到探测链路页 | `MainWindow.xaml` L186–197 无任何事件 |
| A2 | 链路页公共路径把 Mapper 给出的 `Reasons`/`Metrics` **清空**，只剩名字+徽章，看不到「为什么判这层坏」 | `MainWindow.xaml.cs` L671–672 写死 `""`；`ChainMapper` 明明产出了原因 |
| A3 | AI/目标卡片单击行为是**打开浏览器官网**，与「定位断点」主任务无关，且无任何页内详情 | `Card_MouseLeftButtonUp` → `TryOpen(url)` |
| A4 | 链路页「趋势」跳转后只显示提示横幅，图表仍是网关/国内/海外汇总，**分目标序列未实现** | `OpenTrendForTarget` L62–70 |

### B. 操作反馈缺失 — 点了没反应

| # | 问题 | 证据 |
|---|------|------|
| B1 | 主窗「立即探测」点击后**无任何 busy 反馈**，只能等 5s 轮询；托盘反而有「探测中…」+ Balloon | `Probe_Click` 只 `_probeNow?.Invoke()`；防重入 `_probing` 只在 `TrayHost` |
| B2 | 探测中主窗按钮**可重复点击**（托盘侧吞并发，但 UI 无禁用态） | 同上 |
| B3 | 「刷新」无 busy；`RefreshAsync` 出错只把错误写进总览 headline，其它页悄然停更 | `RefreshAsync` catch L261–264 |
| B4 | 设置入口**模态不一致**：主窗 `ShowDialog`、托盘 `Show` 非模态，可同时开两个设置窗互相覆盖保存 | `MainWindow.xaml.cs` L230 vs `TrayHost.cs` L250 |

### C. 信息架构 — 重复与噪音

| # | 问题 | 证据 |
|---|------|------|
| C1 | AI / 自定义目标 / 本机网络在**总览与专页各渲染一份**，专页增量价值只有 Targets 的编辑功能 | `ApplyOverview` vs `ApplyAi`/`ApplyLocal` |
| C2 | 总览 `OverviewProxy` 控件存在但**恒被清空**（信息已挪去 Meta），留下空白区 | XAML L352；cs L579 |
| C3 | 告警/趋势页只在可见时刷新，切页靠 `ShowPage` 强刷——机制可用，但导航项上**没有未读徽标**，用户不知道该不该去看告警页 | `RefreshAsync` L256–259 |

### D. i18n 与主题一致性

| # | 问题 | 证据 |
|---|------|------|
| D1 | 英文界面下状态徽章仍是中文「正常/失败/降级」（`ShortState` 硬编码），`ShortStateFromDetail` 靠**中文子串匹配**判状态 | `MainWindow.xaml.cs` L701–722 |
| D2 | 首帧徽章绿底、趋势图例、出口切换徽章等**浅色 hex 写死在 XAML**，深色主题首帧闪白 | `MainWindow.xaml` L117–119、L575–582；`TargetFlowBlock.xaml` L35–38 |
| D3 | 告警严重度 soft 底色（`#FCE8E6` 等）**无深色分支**，深色主题下刺眼 | `AlertSeverityColors` L427–433 |
| D4 | 语义色散落两处：`ThemeApplier` 注入一套键、code-behind `Brush()/SoftBrush()` 再手工换算一套，没有统一 token | `ThemeApplier.cs` + `MainWindow.xaml.cs` 多处 |
| D5 | 空状态按钮样式不统一（告警页用 `NsProbeButton`，链路页 `EmptyProbeButton` 是裸 Button） | `NetworkFlowControl.xaml` L15–17 |

---

## 3. HandyControl 深度美化方案

原则（ponytail）：**零新依赖**。HandyControl 3.5.1 已在包里，下面全部是「把已付的钱花掉」；WinForms 托盘、LiveCharts、链路 Canvas 动画能跑就不动。

### 3.1 控件升级清单（现状 → HC 方案）

| 现状 | HC 方案 | 解决的问题 | 涉及文件 |
|------|---------|-----------|----------|
| 「立即探测」普通 Button，无 busy | **`hc:ProgressButton`**（IsChecked = 探测中）或 Button + `hc:LoadingCircle` 叠加；探测期间禁用并改文案 | B1 B2 | `MainWindow.xaml` 侧栏 + 各空状态按钮 |
| 探测完成/失败无窗内通知 | **`Growl.Success/Error`**（主窗右侧容器 `hc:Growl.GrowlParent`），替代「只有托盘 Balloon」 | B1 B3 | `MainWindow.xaml` 根 Grid 加 GrowlPanel；`TrayHost` 探测回调 |
| 空状态 = 手写 TextBlock + 按钮 | **`hc:Empty.ShowEmpty`** 附加属性统一各 ItemsControl/List 空态；按钮统一 `NsProbeButton` | D5、体验一致 | 链路 / 告警 / 趋势 / 目标页 |
| 链路「公共路径」= 横排彩色卡片 | **`hc:StepBar`**（水平），每步 = 一层，步条颜色跟层状态；`hc:Poptip` 悬停展示 `Reasons`/`Metrics` | A2（原因重新可见）+ 视觉升级 | `MainWindow.xaml` PageChain、`ApplyChain` |
| 导航项无告警提示 | **`hc:Badge`** 包住「通知告警」SideMenuItem，显示未读数（新告警时 +1，进页清零） | C3 | `MainWindow.xaml` 导航区 |
| 页面切换生硬（Visibility 直切） | **`hc:TransitioningContentControl`** 包内容区，切页 160ms 过渡（尊重系统「减少动画」则关闭） | 观感 | `ShowPage` |
| 长页无回顶 | **`hc:GoToTop`** 挂总览/告警 ScrollViewer | 观感 | `MainWindow.xaml` |
| 卡片悬停提示用原生 ToolTip / 无提示 | **`hc:Poptip`** 统一（AI 卡片显示直连/代理明细，层卡片显示原因） | A2 A3 辅助 | 各卡片模板 |
| 区块分隔靠 Margin | **`hc:Divider`**（带标题分隔线）替代部分 Expander 标题 | 视觉层次 | 总览 |
| 设置双入口模态打架 | 统一为非模态单例：已开则 `Activate()`；主窗与托盘走同一入口 | B4 | `MainWindow.xaml.cs`、`TrayHost.cs` |

不动的（评估过，YAGNI）：

- **WinForms `NotifyIcon` 不换 `hc:NotifyIcon`**：现有托盘（菜单、Balloon、双击）稳定工作，换 HC 版要引入 `TaskbarRebuildBehavior` 等新面，收益只有观感统一，风险不对称。
- **LiveCharts 不换**：HC 无同级图表控件。
- **链路 Canvas 动画不重写**：DESIGN.md 已约束其形态。

### 3.2 语义色 token 化（修 D2–D4 的根）

把散落的状态色收敛为一套 App 级资源键，浅/深两份，由 `ThemeApplier` 随 Skin 一起切换：

```
NsStatusOkBrush / NsStatusOkSoftBrush          #137333 / #E8F5E9   ·  dark: #81C995 / #1E3A2A
NsStatusDegradedBrush / ...SoftBrush           #B06000 / #FEF7E0   ·  dark: #FDD663 / #3D3320
NsStatusFailBrush / ...SoftBrush               #C5221F / #FCE8E6   ·  dark: #F28B82 / #3C2523
NsStatusSkippedBrush / ...SoftBrush            #5F6368 / #F1F3F4   ·  dark: #9AA0A6 / #2A2E35
```

- 值与 DESIGN.md §2 对齐；soft 深色值为新增（解决 D3）。
- XAML 首帧一律 `DynamicResource`，删除写死的 `#E8F5E9`/`#34A853` 等（D2）。
- code-behind 的 `Brush()/SoftBrush()` 换算函数退役，`CardVm.AccentBrush` 改为按状态查资源键（D4）。
- `AlertSeverityColors` 同步改为查资源。

### 3.3 业务流修复（与美化同批，小 diff）

| # | 修复 | 做法 |
|---|------|------|
| F1 | 总览层卡片可下钻（A1） | 层卡片加 `MouseLeftButtonUp` → `ShowPage("chain")` + `NavMenu` 选中同步；Cursor=Hand + hover 底色变化提示可点 |
| F2 | 公共路径恢复原因（A2） | StepBar/Poptip 方案自带；至少把 `Reasons` 塞回 `ChainRowVm` |
| F3 | 探测全链路反馈（B1–B3） | `TrayHost.RequestProbe` 暴露探测状态事件 → 主窗 ProgressButton busy + 完成 Growl；`_probing` 状态双端共享 |
| F4 | 状态词走 `UiStrings`（D1） | `ShortState`/`ShortStateFromDetail` 改为 `UiStrings.StateName(lang, …)`；删掉中文子串匹配，直接用 `DirectState/ProxyState` 的结构化值判 |
| F5 | 总览瘦身（C1 C2） | 删除恒空的 `OverviewProxy`；总览 AI/目标/本机区各保留摘要行 + 「详情 →」跳专页，明细列表只在专页渲染 |
| F6 | 告警未读徽标（C3） | Badge 数 = 上次进入告警页后新增条数，存内存即可 |

（A4 分目标趋势序列涉及样本聚合改造，**不进本期**，留在 ROADMAP「更丰富的趋势指标」。）

### 3.4 逐页美化要点

- **总览**：Overall 大徽章改用 token 色；`hc:Divider` 分区；告警条内嵌 Badge 计数；层卡片 hover/可点样式。
- **探测链路**：StepBar 公共路径 + Poptip 原因；目标块头部徽章统一 `hc:Tag` + token 色。
- **AI / API**：卡片直连/代理两行加图标化 `hc:Tag`；Poptip 展示分段时延（dns/connect/tls/firstByte 已有数据）。
- **自定义目标**：输入区改 `hc:SearchBar` 风格或保留 TextBox + `hc:InfoElement.Placeholder`；上限提示用 Growl.Warning 而非静默截断。
- **历史趋势**：窗口切换 Radio 改 `hc:ButtonGroup`；图例色改从 token 取。
- **通知告警**：severity 筛选改 `hc:ButtonGroup`；列表空态 `hc:Empty`；soft 色走 token。
- **本机网络**：KV 列表加 `hc:Divider` 分组（接口 / DNS / 代理 / Wi-Fi）。
- **设置**：单例非模态；保存成功 Growl.Success 取代无声关闭。

### 3.5 动效约束

沿用 DESIGN.md §6：页面过渡 ≤180ms ease-out；Growl 用默认进出场；系统关闭动画时 TransitioningContentControl 退化为直切。不新增循环动画。

---

## 4. 实施分期与验收

| 步 | ID | 内容 | 验收 |
|----|----|------|------|
| 1 | W11a | ✅ 语义色 token 化（§3.2）+ 删 XAML 硬编码 | `StatusTokens` + `ThemeApplier` 注入；XAML 首帧走 DynamicResource |
| 2 | W11b | ✅ 探测反馈链（F3：ProgressButton + Growl + 防重入） | 主窗 ProgressButton busy + Growl；托盘共享 `_probing` |
| 3 | W11c | ✅ 总览下钻 + 公共路径原因恢复（F1 F2） | 层卡片可点进链路；Trunk 显示 Reasons + ToolTip |
| 4 | W11d | ✅ i18n 修复（F4）+ 空状态按钮统一 | `ShortState` 走 `UiStrings`；链路空态用 `ButtonPrimary` |
| 5 | W11e | ✅ 信息架构瘦身 + 告警 Badge（F5 F6）+ 设置单例（B4） | 总览摘要+详情跳转；`hc:Badge`；设置非模态单例 |
| 6 | W11f | ◐ 部分 | 总览已挂 `hc:GotoTop`；TransitioningContentControl / ButtonGroup 留后续（低优先级） |

每步独立可合并；构建后按惯例重启 `NetStrata.exe` 做烟雾验证。测试约束沿用 WPF-ROADMAP：不真启 GUI，可测的纯逻辑（token 查表、状态词映射、Badge 计数）各留一个断言测试。

## 5. 不做（YAGNI）

- 不换 UI 框架、不引 WPF-UI/MahApps 等第二套皮肤。
- 不重写托盘为 `hc:NotifyIcon`。
- 不做 MVVM 全量重构（拆 ViewModel 文件是另一议题，与美化解耦）。
- 分目标趋势序列（A4）不进本期。
