# NetStrata

> **Windows 原生分层网络健康监控** — 告诉你故障在哪一层：Wi-Fi、局域网、国内宽带、国际直连、代理、AI API。

NetStrata 是受 [canireach](https://github.com/canireach/canireach) 启发的 .NET 重写项目，专为 **Windows** 设计，解决原版在 Windows 上因依赖 macOS 命令（`dig`、`scutil`、`/sbin/ping`）而无法正常工作的问题。

## 快速开始

```powershell
git clone git@github.com:kongliuli/NetStrata.git
cd NetStrata
dotnet test
dotnet run --project src/NetStrata.Tray -- --once

# 托盘 UI + 进程内 Daemon（默认）
dotnet run --project src/NetStrata.Tray

# 单文件发布
.\scripts\publish.ps1
.\artifacts\publish\NetStrata.exe
.\artifacts\publish\NetStrata.exe --help
.\artifacts\publish\NetStrata.exe --once
```

完整用法见 **[docs/USAGE.md](docs/USAGE.md)**；CLI / 任务计划 / Cursor Loop 见 **[docs/SCHEDULING.md](docs/SCHEDULING.md)**。

## 产品定位

| 维度 | 说明 |
|------|------|
| **目标用户** | 需要监控网络稳定性、代理链路健康、AI API 可达性的 Windows 开发者 |
| **核心价值** | 分层定位故障，而非笼统的「网断了」 |
| **运行形态** | **WPF 主窗 + 托盘（主）** + 同 exe CLI / TUI |
| **技术栈** | .NET 8+ / C# / WPF / HandyControl |

## CLI 速查（同一 `NetStrata.exe`）

| 命令 | 说明 |
|------|------|
| `NetStrata` | 主窗 + 托盘 + 进程内 Daemon（默认） |
| `NetStrata --tui` | TUI |
| `NetStrata --follow` | TUI 只读 daemon state |
| `NetStrata --once` | 单次探测，JSON 到 stdout |
| `NetStrata --once --ping IP` | 附加自定义 ping |
| `NetStrata --export -o report.md` | 导出诊断报告 |
| `NetStrata --help` | 帮助 |

> `--web` 本阶段未启用；探测由托盘进程内 Daemon 完成。Web 仪表盘后续再做。

## 环境变量

| 变量 | 默认 | 说明 |
|------|------|------|
| `NETSTRATA_INTERVAL_MS` | `60000` | 探测间隔 |
| `NETSTRATA_PORT` | `8787` | 预留（Web 后续） |
| `NETSTRATA_PROXY` | auto | 代理 URL；`none`/`off` 禁用 |
| `NETSTRATA_PING_EXTRA` | — | 额外 ping（逗号分隔，最多 10） |
| `NETSTRATA_CONCLUSION_EVERY` | `30` | 每 N 轮写 conclusions.md |
| `NETSTRATA_DOWNLOAD_EVERY` | `10` | 每 N 轮测代理下载 |
| `NETSTRATA_LANG` | auto | TUI 语言 `zh`/`en` |

配置：`%APPDATA%\NetStrata\config.json`（示例 [docs/config.example.json](docs/config.example.json)）。

## 文档索引

| 文档 | 内容 |
|------|------|
| **[docs/USAGE.md](docs/USAGE.md)** | **安装、命令、配置、数据目录、验收** |
| **[docs/SCHEDULING.md](docs/SCHEDULING.md)** | **CLI/任务计划/Cursor Loop** |
| [docs/PRODUCT.md](docs/PRODUCT.md) | 产品定义、命名 |
| [docs/SPEC.md](docs/SPEC.md) | 探测项与判决规则 |
| [docs/DATA-MODEL.md](docs/DATA-MODEL.md) | Sample / Verdict JSON |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 解决方案结构 |
| [docs/WINDOWS.md](docs/WINDOWS.md) | Windows 探测对照 |
| [docs/LAYER3.md](docs/LAYER3.md) | TLS 栈、告警、导出 |
| [docs/TESTING.md](docs/TESTING.md) | 测试规格 |
| [docs/ROADMAP.md](docs/ROADMAP.md) | 分期计划 |
| [docs/WPF-ROADMAP.md](docs/WPF-ROADMAP.md) | WPF 与单 exe 架构 |
| [scripts/README.md](scripts/README.md) | agent-loop、health-check |

## 调度与 Agent

- **CLI**：`NetStrata --once` / `--export` 适合脚本与 CI
- **文件**：`%APPDATA%\NetStrata\data\state.json`、`samples.jsonl`
- **开发循环**：`scripts/agent-loop.ps1` 或 Cursor `/loop 5m`
- **健康检查**：`scripts/health-check.ps1`

## 仓库状态

**Phase 0–5 已完成**（含 Layer 3：TLS 栈、RouteWatch 告警、导出、结论引擎）。

**Phase W6 已完成**；**单 exe 重构已完成**：WPF 托盘为主入口，进程内 Daemon，CLI 为同 exe 子命令。Web 仪表盘延后。

## 许可证

MIT

## 致谢

探测分层模型与判决逻辑参考 [canireach](https://github.com/canireach/canireach)（MIT）。
