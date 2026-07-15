# 监控目标流量可视化

## 模型

每个 HTTPS 监控目标是**独立的可展开动画块**：

```text
[▼ OpenAI]  出口：代理 · http://127.0.0.1:7890
   Wi-Fi → 局域网 → 宽带 → 代理出口 → OpenAI

[▶ GitHub]  出口：直连
   （折叠）
```

- 标题折叠态即可看到该目标的**流量出口**（直连 / 代理，可互不相同）
- 展开后播放该目标自己的路径动画
- 出口选择：该目标真实 HTTPS 结果（直连失败且代理成功 → 代理；双通取更快者）

## 数据

`MultiTargetFlowBuilder.FromState` → `TargetPathBlock[]` → `NetworkFlowControl.SetBlocks` → 多个 `TargetFlowBlock`
