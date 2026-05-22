# DevNexus AI 文档中心

本目录承载与当前代码实现一致的长期文档。事实源以 `src/` 代码、Swagger、DTO、枚举和配置模型为准。

## 快速导航

| 读者目标 | 推荐文档 |
|----------|----------|
| 第一次运行项目 | [安装指南](./00-getting-started/installation.md)、[快速开始](./00-getting-started/quickstart.md) |
| 了解项目能力 | [项目概览](./01-overview/project-overview.md)、[功能特性](./01-overview/features.md) |
| 理解架构边界 | [架构索引](./02-architecture/README.md) |
| 配置本地或部署环境 | [配置指南](./03-guides/configuration-guide.md) |
| 对接接口或 SignalR | [API 规格](./04-api/api-specification.md) |
| 参与开发 | [开发指南](./06-development/README.md)、[贡献说明](../CONTRIBUTING.md) |
| 处理安全事项 | [安全策略](../SECURITY.md) |

## 文档结构

### `00-getting-started`

- `installation.md` — 本地环境、依赖服务、构建命令和启动入口
- `quickstart.md` — 最短可验证运行路径
- `first-steps.md` — 聊天、文件和 Swarm 的能力边界

### `01-overview`

- `project-overview.md` — 项目定位、模块边界和主入口
- `features.md` — 功能清单
- `use-cases.md` — 典型使用场景与路径

### `02-architecture`

- `01-swarm-architecture.md` — Swarm 上下文工作包架构
- `02-semantic-document-model-design.md` — SmartDocument 语义模型
- `03-semantic-document-ingestion.md` — 文档摄取链路
- `04-search-pipeline-and-repo-parser.md` — 搜索与仓库解析
- `05-chat-orchestration-boundaries.md` — 聊天编排边界
- `06-agent-runtime-architecture.md` — Agent Runtime 架构
- `07-cli-runtime-stability-architecture.md` — CLI Runtime 稳定性架构
- `08-user-auth.md` — 用户与认证分层边界
- `09-auth-token-model.md` — 访问令牌与刷新令牌模型

### `03-guides`

- `configuration-guide.md` — 配置项、密钥、连接串和部署约定
- `file-runner-contract.md` — FileTask 外部 Runner 输入输出合同
- `release-and-update-operations.md` — 客户端版本发布与更新管理
- `user-guide.md` — 聊天、上传、文件任务和结果处理

### `04-api`

- `api-specification.md` — REST 控制器、SignalR Hub、Provider 枚举和鉴权约定

### `05-design`

- `01-client-ui-design.md` — 共享客户端 UI 设计规范
- `02-provider-management.md` — Provider 管理模型和接口
- `03-system-experience.md` — 聊天、文件、Swarm 的体验设计

### `06-development`

- `setup.md` — 开发环境入口
- `coding-standards.md` — 编码规范
- `testing.md` — 构建与校验
- `contributing.md` — 贡献流程

### `07-faq`

- `general.md` — 概念与设计
- `installation.md` — 安装与启动
- `usage.md` — 使用与排障

## 维护规则

- 文档只描述当前代码中存在的能力
- 文件名反映正文主题；移动或重命名时同步更新引用
- 接口、DTO、枚举、配置键变化后，同步更新 API、配置和架构文档
