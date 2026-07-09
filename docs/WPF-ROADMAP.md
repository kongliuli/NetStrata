# NetStrata.Tray — WPF 路线图

> **与 Cli 共存**：`NetStrata.Cli`（控制台/TUI/Web）与 `NetStrata.Tray`（WPF 桌面）共享 `NetStrata.Core`，各自独立 exe，可单独发布。

## 原则

1. **每步可单独合并** — 一个 Phase 只做一件事，跑通 `dotnet build` + 相关测试
2. **不是薄壳** — UI、状态机、Daemon 生命周期在 Tray 项目内完整实现；Core 只放可复用逻辑
3. **单元测试不启动 GUI** — 托盘逻辑抽 `TrayStatusMapper`、`TrayStateReader` 在 Core 测；WPF 仅手动冒烟

---

## 项目结构

```
src/
  NetStrata.Core/       # 探测、判决、存储、Tray 逻辑
  NetStrata.Daemon/     # 后台采集
  NetStrata.Cli/        # netstrata.exe
  NetStrata.Tray/       # netstrata-tray.exe
```

---

## Phase W6 — 分步实现

| 步 | ID | 状态 | 交付物 | 验收 |
|----|-----|------|--------|------|
| 1 | **W6a** | ✅ 完成 | 托盘 + 轮询 state.json + 基础菜单 | 颜色随 overall 变 |
| 2 | **W6b** | ✅ 完成 | CliPathResolver、OnceProbeRunner、气泡提示 | 菜单显示周期、探测结果 balloon |
| 3 | **W6c** | ✅ 完成 | 原生 WPF Dashboard 窗口 | 六层 + 代理 + 告警 |
| 4 | **W6d** | ✅ 完成 | 设置窗口 | 编辑 config.json（interval/port/ping/TLS） |
| 5 | **W6e** | ✅ 完成 | Daemon 启停 | 托盘内 --web 生命周期 + 端口检测 |
| 6 | **W6f** | 待做 | Toast 告警 | recentAlerts 变化通知 |
| 7 | **W6g** | 待做 | 开机自启 | Startup 快捷方式（可选） |
| 8 | **W6h** | 待做 | 发布 | netstrata-tray win-x64 单文件 |

---

## W6a 验收

```powershell
dotnet run --project src/NetStrata.Cli -- --web
dotnet run --project src/NetStrata.Tray
# 托盘圆点应随 state.json 更新
```
