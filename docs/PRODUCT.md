# 产品定义

## 名称

| 项 | 值 |
|----|-----|
| **英文名** | NetStrata |
| **CLI 命令** | `netstrata` |
| **含义** | Strata（地层/层级）+ Net — 分层透视网络健康 |
| **中文工作名** | 络层（内部简称，对外统一用 NetStrata） |

### 命名理由

- **分层**是核心差异化：不是 ping 一个地址，而是 6 层递进判决
- **Strata** 在英文中有地质学「地层」含义，准确传达「逐层下钻定位」
- 与 canireach 区分，避免商标/包名冲突
- 简短、可注册 npm/NuGet 包名：`NetStrata`

### 避免的名称

| 候选 | 放弃原因 |
|------|----------|
| CanIReach / canireach | 原版已有，且 macOS 导向 |
| NetPulse | 过于泛化，不体现分层 |
| WinReach | 平台绑定感太强 |

---

## 一句话描述

> NetStrata 是 Windows 上的分层网络诊断工具，持续监控 Wi-Fi 到 AI API 的每一跳，告诉你哪一层出了问题。

---

## 与 canireach 的关系

| 维度 | canireach | NetStrata |
|------|-----------|-----------|
| 平台 | macOS / Linux 优先 | **Windows 原生** |
| 运行时 | Node / Bun 单文件 | **.NET 8+ 自包含发布** |
| 探测实现 | 调用 `dig`、`curl`、`scutil` | **.NET API + 系统命令** |
| 分层模型 | 6 层 + AI headline | **继承并扩展** |
| 数据格式 | `samples.jsonl` | **兼容 schema，可增量扩展** |
| Web UI | 自带 Chart.js 前端 | **初期复用/移植，后期可 Blazor** |

**不是 fork**：逻辑参考、实现重写、平台特化。

---

## 设计原则

1. **分层先于总量** — 永远先回答「哪一层」，再回答「通不通」
2. **skipped 不是 fail** — 有线网跳过 Wi-Fi 层；无代理跳过代理层
3. **直连与代理分离** — HTTPS 探测必须显式禁用/启用代理，不能混测
4. **Windows 第一** — 不依赖 WSL、不假设 Unix 工具存在
5. **最小可运行** — 每个 Phase 结束都有可执行的 CLI 检查点
6. **数据可持久化** — `samples.jsonl` 支持历史回溯与图表

---

## 目标用户场景

### 场景 A：代理 + AI 开发

用户开 Clash Verge / mihomo，需要确认：
- 代理端口是否在监听
- 代理出口 IP 是否变化
- `api.anthropic.com` / `api.openai.com` 走代理是否可达

### 场景 B：网络不稳定

Wi-Fi 信号差 vs 路由器问题 vs 运营商故障 — 需要分层定位。

### 场景 C：纯直连（无代理）

代理层 `skipped`，只看国内宽带 + 国际直连 + AI 直连可达性。

### 场景 D：TUN 模式

Clash TUN 不走系统代理 — NetStrata 需支持端口扫描 + 手动 `NETSTRATA_PROXY` 配置（见 [WINDOWS.md](WINDOWS.md)）。

---

## 非目标（Out of Scope）

- 不做 VPN 客户端 / 代理管理
- 不做远程监控 SaaS（仅本地）
- 不做流量抓包 / 深度包检测
- 第一期不做 macOS / Linux 支持

---

## 成功标准

| 指标 | 标准 |
|------|------|
| 探测准确性 | 在 Windows 11 上，网络正常时 `overall=healthy` |
| 分层定位 | 断开代理后 `proxy` 层 fail，其他层不受影响 |
| 性能 | 单轮探测 < 15s（20s 间隔下留足余量） |
| 发布 | `dotnet publish` 单文件 exe，无需安装 .NET Runtime |
