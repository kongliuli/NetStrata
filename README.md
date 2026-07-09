# NetStrata

> **Windows 原生分层网络健康监控** — 告诉你故障在哪一层：Wi-Fi、局域网、国内宽带、国际直连、代理、AI API。

NetStrata 是受 [canireach](https://github.com/canireach/canireach) 启发的 .NET 重写项目，专为 **Windows** 设计，解决原版在 Windows 上因依赖 macOS 命令（`dig`、`scutil`、`/sbin/ping`）而无法正常工作的问题。

## 产品定位

| 维度 | 说明 |
|------|------|
| **目标用户** | 需要监控网络稳定性、代理链路健康、AI API 可达性的 Windows 开发者 |
| **核心价值** | 分层定位故障，而非笼统的「网断了」 |
| **运行形态** | CLI 工具 + 本地 Web 仪表盘 + 可选 TUI |
| **技术栈** | .NET 8+ / C# |

## 两个核心问题

1. **网络通不通？** — Wi-Fi → 局域网 → 国内宽带 → 国际直连 → 代理，逐层判决
2. **AI API 通不通？** — Anthropic / OpenAI 直连与代理路径分别可达性

## 文档索引

开发前请按顺序阅读：

| 文档 | 内容 |
|------|------|
| [docs/PRODUCT.md](docs/PRODUCT.md) | 产品定义、命名、设计原则、与 canireach 的关系 |
| [docs/SPEC.md](docs/SPEC.md) | 功能规格：探测项、目标列表、判决规则 |
| [docs/DATA-MODEL.md](docs/DATA-MODEL.md) | Sample / Verdict JSON 数据结构 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | .NET 解决方案结构、模块划分、依赖 |
| [docs/WINDOWS.md](docs/WINDOWS.md) | Windows 平台探测实现对照表 |
| [docs/API.md](docs/API.md) | Web 仪表盘 HTTP API |
| [docs/ROADMAP.md](docs/ROADMAP.md) | 分期开发计划与验收标准 |
| [docs/LAYER3.md](docs/LAYER3.md) | 第三层增强：TLS 栈、告警、导出、自定义 Ping |
| [docs/TESTING.md](docs/TESTING.md) | 测试先行规格与用例表 |

## 计划中的 CLI 用法

```powershell
# 单次探测，输出 JSON
netstrata --once

# 单次探测并 ping 自定义目标（内网 NAS 等）
netstrata --once --ping 192.168.1.50,10.0.0.1

# 导出最近 1 小时报告（Layer 3）
netstrata --export --minutes 60 -o report.md

# Web 仪表盘（默认 http://localhost:8787）
$env:NETSTRATA_NO_OPEN='1'
$env:NETSTRATA_INTERVAL_MS='20000'
netstrata --web

# 终端 TUI（后期）
netstrata
```

## 环境变量（规划）

| 变量 | 默认 | 说明 |
|------|------|------|
| `NETSTRATA_INTERVAL_MS` | `60000` | 探测间隔（毫秒） |
| `NETSTRATA_PORT` | `8787` | Web 端口 |
| `NETSTRATA_PROXY` | auto | 强制代理 URL；`none`/`off` 禁用 |
| `NETSTRATA_LANG` | auto | `zh` / `en` |
| `NETSTRATA_DOWNLOAD_EVERY` | `10` | 每 N 轮测代理下载速度 |
| `NETSTRATA_NO_OPEN` | `0` | `1` = 不自动打开浏览器 |
| `NETSTRATA_PING_EXTRA` | — | 逗号分隔额外 ping 目标，最多 10 个 |
| `NETSTRATA_CONCLUSION_EVERY` | `30` | 每 N 轮生成 conclusions.md（Layer 3） |

## 仓库状态

**当前阶段：文档驱动开发（Documentation Phase）**

- [x] 产品命名与规格文档
- [x] Layer 3 / 测试先行 / 自定义 Ping 规格
- [x] Phase 1 MVP（`netstrata --once`）
- [x] Phase 2 Windows 平台适配
- [x] Phase 3 Web 仪表盘 + Daemon
- [ ] Phase 4 TUI + 增强功能

## 许可证

MIT

## 致谢

探测分层模型与判决逻辑参考 [canireach](https://github.com/canireach/canireach)（MIT）。
