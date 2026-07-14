# 多层网络流转可视化

## 目标

NetStrata 在 WPF 主窗口的“探测链路”页面提供三种可播放视图：

1. 分层诊断：设备 / Wi-Fi、局域网、宽带、海外直连、代理、AI 目标。
2. 直连 / 代理：从本机到目标的两条诊断路径对照。
3. TLS 栈：DNS、TCP、TLS、HTTP 的真实顺序阶段。

界面固定显示“诊断结果重放，非逐包抓取”。六层模型是诊断依赖抽象，探针可能并行执行。只有 `TlsStackResult` 的 DNS、TCP、TLS、HTTP 是严格顺序执行。

## 为什么使用原生 WPF

本功能不需要 JavaScript、WebView2 或独立 Web 服务。原生 WPF 已经具备全部所需能力：

- `Canvas` 放置节点和双泳道分支。
- `Line` / `PathGeometry` 绘制连接关系。
- `TranslateTransform` + `DoubleAnimation` 移动路径标记。
- HandyControl 动态资源继承浅色、深色主题。
- WPF Automation 暴露按钮、状态和实时说明。

原生实现不会增加浏览器运行时和前后端通信边界，单文件发布方式保持不变。

## 数据流

```text
state.json
  -> JsonSampleStorage.ReadStateAsync
  -> DaemonState
  -> FlowTraceBuilder
     -> Layers FlowTrace
     -> Routes FlowTrace
     -> TLS FlowTrace
  -> NetworkFlowControl
  -> 静态布局 + 用户触发的结果重放
```

`DaemonState` 仍是唯一事实源。动画只解释已有数据，不产生新的健康结论。

## 代码结构

```text
src/NetStrata.Core/Flow/
  FlowTrace.cs
  FlowTraceBuilder.cs

src/NetStrata.Tray/Views/Controls/
  NetworkFlowControl.xaml
  NetworkFlowControl.xaml.cs
  NetworkFlowLayout.cs

tests/NetStrata.Core.Tests/Flow/
  FlowTraceBuilderTests.cs
```

主窗口在每次 `RefreshAsync()` 后调用：

```csharp
NetworkFlow.SetTraces(FlowTraceBuilder.FromState(state, lang));
```

如果播放期间出现新采样，控件暂存新 `FlowTrace`，等待本轮播放结束后替换，避免路径在动画中途跳变。

## 三种映射

### 分层诊断

```text
设备 / Wi-Fi -> 局域网 -> 宽带 -> 海外直连 -> AI 目标
                              -> 代理 ------> AI 目标
```

状态来自 `Verdict.Layers`。缺少某层时显示“待确认”，不能默认成功。此模式不显示伪造的层耗时。

### 直连 / 代理

```text
本机 -> 直连 -> 目标
    -> 代理 -> 目标
```

映射器从 `HttpsResult.Label` 的 `_direct` 和 `_proxy` 后缀选择代表性结果，优先展示成功且耗时最低的结果。只要任一路径可用，目标节点显示通过；两路都失败时显示失败。该视图表达结果对照，不保证两条请求同时开始。

### TLS 栈

```text
DNS -> TCP -> TLS -> HTTP
```

每个节点直接使用 `TlsStackLayerResult.Ms`。`stoppedAt` 对应阶段显示失败，后续缺失阶段显示“未执行”。这是唯一表达严格执行顺序的模式。

## 动画规则

- 初次打开、刷新和切换模式都不自动播放。
- 节点按 `Stage` 分组。直连和代理位于同一阶段，因此同时进入活动态。
- 移动标记只播放一次，不循环、不闪烁。
- 动画时长由真实耗时映射：`clamp(220, 90 + sqrt(measuredMs) * 45, 900)`。
- 没有真实耗时的阶段使用 320 ms，但界面不显示虚构数值。
- 速度控制提供 0.5x、1x、2x。
- Windows 关闭客户端动画时，路径移动降级为离散状态切换。
- 暂停保留已完成阶段，继续时从当前阶段重新播放。

## 响应式布局

控件以 700 设备独立像素为分界：

- 宽布局：横向主路径，直连和代理上下分支。
- 窄布局：纵向主路径，分支节点并排放置。

布局切换时暂停播放并重新绘制，避免动画与几何位置不同步。

## 主题与可访问性

- 表面、边框、正文和弱化文字使用 HandyControl 动态资源。
- 成功、降级、失败、未执行同时使用颜色和文字。
- 模式、播放、重置、速度使用原生 WPF 控件和键盘行为。
- 节点的 Automation 名称包含标签、状态、耗时和详细原因。
- 当前步骤使用礼貌级实时通知。
- Windows 减少动画设置生效。

完整视觉规范见仓库根目录的 [`DESIGN.md`](../DESIGN.md)。

## 验证

Core 测试覆盖六层状态映射、直连失败而代理成功、TLS 失败后后续阶段跳过。WPF 验收覆盖常规和窄窗口、三种模式、播放控制、浅色和深色主题、键盘焦点以及新采样延迟切换。

## 后续扩展

当前版本重放完整快照。若需要探针执行中的实时进度，可在同一进程内增加有界 `Channel<ProbeEvent>`。事件只驱动临时进度，周期结束后仍以 `DaemonState` 为最终事实源，不需要 WebSocket。
