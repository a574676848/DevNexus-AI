# DevNexus AI

[![MIT License][license-shield]][license-url]
[![.NET][dotnet-shield]][dotnet-url]
[![Blazor][blazor-shield]][blazor-url]

DevNexus AI 是一个本地优先、可私有化部署的 AI 工作站。提供实时聊天、多供应商模型管理、文档语义摄取、文件资产与任务运行时、Swarm 多智能体协作，以及 Web / 桌面双客户端。

![系统架构总览](./docs/assets/Architecture_Diagram.png)

## 核心能力

- **实时聊天** — REST + SignalR 组合提供会话管理、排队消息、流式输出、终端运行态与 Artifact 更新
- **供应商管理** — 支持 LLM、Embedding、Search、Storage 的运行时配置、验证、默认项切换与加密存储
- **语义摄取** — 上传文件可生成 SmartDocument，用于摘要、分块、检索和聊天上下文
- **文件资产** — UploadSession、FileAsset、FileVersion、FileTask 构成文件处理主链路
- **外部 Runner** — 文件任务优先调用受控工作区内的 `runner.ps1`，其次调用 `runner.py`，输出文件回灌为新资产
- **Swarm 协作** — 复杂任务可拆分为上下文工作包，支持工作包状态、Agent 状态、暂停/恢复/中止和人机确认
- **多端客户端** — 共享 UI 与状态逻辑，承载 Blazor WebAssembly 和 .NET MAUI 桌面客户端

## 技术栈

| 层级 | 实现 |
|------|------|
| 后端运行时 | .NET 10、ASP.NET Core、SignalR、JWT Bearer |
| 编排与 AI | Semantic Kernel 1.74、Microsoft.Extensions.AI、Kernel Memory |
| 数据与后台任务 | PostgreSQL、EF Core 10、Hangfire、Redis |
| 检索与观测 | Qdrant、Elasticsearch、Seq、OpenTelemetry |
| 前端 | Blazor WebAssembly、.NET MAUI、MudBlazor、Monaco Editor、Plotly.js |
| 宿主 | .NET Aspire AppHost |

## 仓库结构

```text
src/
  backend/
    DevNexus.ApiService/       # REST API、SignalR Hub、中间件和启动入口
    DevNexus.Core/             # 应用编排：聊天、Swarm、文件任务、认证用例
    DevNexus.Domain/           # 领域实体、配置模型和抽象接口
    DevNexus.Infrastructure/   # EF Core、Provider、存储、搜索、Skill、后台任务
  client/
    DevNexus.Client.Shared/    # 共享 Blazor UI、状态、服务和静态资产
    DevNexus.Client.Web/       # WebAssembly 客户端
    DevNexus.Client/           # .NET MAUI 桌面客户端
    DevNexus.Client.Updater/   # 桌面更新辅助程序
  common/
    DevNexus.Shared/           # DTO、枚举、常量与跨端契约
    DevNexus.ServiceDefaults/  # Aspire 默认服务配置
    DevNexus.AppHost/          # Aspire 宿主入口
```

## 快速开始

### 环境要求

- .NET SDK 10
- Git
- PostgreSQL、Redis、Seq、Qdrant、Elasticsearch
- 可选：PaddleOCR 服务（用于图片 / PDF OCR）
- 可选：Visual Studio 2022、Rider 或 VS Code

### 获取代码并构建

```bash
git clone https://github.com/a574676848/DevNexus-AI.git
cd DevNexus-AI
dotnet restore src/DevNexus.sln
dotnet build src/DevNexus.sln
```

### 配置本地密钥与连接串

在 AppHost 项目中使用 `dotnet user-secrets`，避免把真实配置写入仓库：

```bash
cd src/common/DevNexus.AppHost
dotnet user-secrets init
dotnet user-secrets set "Parameters:jwt-key" "replace-with-a-strong-jwt-key"
dotnet user-secrets set "Parameters:encryption-key" "replace-with-base64-aes-key"
dotnet user-secrets set "Parameters:encryption-iv" "replace-with-base64-aes-iv"
dotnet user-secrets set "ConnectionStrings:devnexus" "Host=localhost;Port=5432;Database=devnexus;Username=postgres;Password=CHANGE_ME"
dotnet user-secrets set "ConnectionStrings:redis" "localhost:6379"
dotnet user-secrets set "ConnectionStrings:seq" "http://localhost:5341"
dotnet user-secrets set "ConnectionStrings:qdrant" "http://localhost:6333"
dotnet user-secrets set "ConnectionStrings:elasticsearch" "http://localhost:9200"
```

### 启动服务

```bash
dotnet run --project src/common/DevNexus.AppHost
```

AppHost 通过 Aspire 注入连接串和敏感参数，同时启动 API 与 Web 客户端。访问地址以控制台输出为准。

桌面客户端可单独启动：

```bash
dotnet run --project src/client/DevNexus.Client
```

## 文档

完整文档位于 [`docs/`](./docs/README.md)，包含：

| 分类 | 内容 |
|------|------|
| [安装指南](./docs/00-getting-started/installation.md) | 依赖服务、构建与配置 |
| [快速开始](./docs/00-getting-started/quickstart.md) | 最短可验证运行路径 |
| [项目概览](./docs/01-overview/project-overview.md) | 模块边界与技术定位 |
| [架构文档](./docs/02-architecture/README.md) | Swarm、语义模型、聊天编排、Agent Runtime 等 |
| [配置指南](./docs/03-guides/configuration-guide.md) | 密钥、连接串与部署约定 |
| [Agent / CLI 排障](./docs/03-guides/agent-cli-operations.md) | 长命令、stdin、停止、日志轮询与 Swarm 工作包排障 |
| [记忆治理验收](./docs/03-guides/memory-governance-operations.md) | 系统经验回放、召回收益、上下文污染与可追踪复用检查 |
| [客户端设计规范](./docs/05-design/01-client-ui-design.md) | 聊天、终端、Swarm、审计的低噪产品体验验收口径 |
| [变更批次与发布前收敛](./docs/06-development/change-batching-and-release-checklist.md) | 提交拆批、验证范围和回滚检查 |
| [API 规格](./docs/04-api/api-specification.md) | REST 控制器、SignalR Hub、Provider 枚举 |

## API 入口

- REST API：`/api/v1/*`，更新接口：`/api/update*`
- Swagger：开发环境下 `/swagger`
- SignalR Hub：`/chat-hub`、`/artifact-hub`、`/swarm-hub`

## 贡献

提交改动前请阅读 [CONTRIBUTING.md](./CONTRIBUTING.md)。

## 安全

真实密钥、访问令牌、证书和数据库连接串不应进入仓库。安全问题请参阅 [SECURITY.md](./SECURITY.md)。

## 许可证

[MIT License](./LICENSE)

## 致谢

DevNexus AI 依赖 .NET、ASP.NET Core、Blazor、.NET MAUI、Semantic Kernel、Qdrant、Elasticsearch、Seq、MudBlazor、Monaco Editor、Plotly.js 等开源项目。

[license-shield]: https://img.shields.io/github/license/a574676848/DevNexus-AI.svg?style=for-the-badge
[license-url]: https://github.com/a574676848/DevNexus-AI/blob/main/LICENSE
[dotnet-shield]: https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white
[dotnet-url]: https://dotnet.microsoft.com/
[blazor-shield]: https://img.shields.io/badge/Blazor-512BD4?style=for-the-badge&logo=blazor&logoColor=white
[blazor-url]: https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor
