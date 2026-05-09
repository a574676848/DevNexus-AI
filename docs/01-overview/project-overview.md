# DevNexus AI 项目概览

DevNexus AI 是一个面向开发与知识工作场景的私有化 AI 工作站。它把模型供应商、实时聊天、语义文档、文件资产、外部执行器和 Swarm 协作放在同一套 .NET 应用中管理。

## 当前定位

当前代码直接支撑五条主线：

1. 聊天会话、队列、流式输出和 Artifact 展示。
2. Provider 管理与加密存储。
3. 文件上传、语义解析和知识检索。
4. 文件资产、文件版本和 FileTask 外部 Runner。
5. Swarm 上下文工作包协作。

## 模块边界

| 模块 | 职责 |
|------|------|
| `DevNexus.ApiService` | REST 控制器、SignalR Hub、认证、中间件、Swagger、数据库迁移启动 |
| `DevNexus.Core` | 聊天、Swarm、文件任务、认证用例、更新管理等应用编排 |
| `DevNexus.Domain` | 领域实体、配置模型、抽象接口 |
| `DevNexus.Infrastructure` | EF Core、Provider、存储、搜索、Skill、后台任务、外部服务接入 |
| `DevNexus.Shared` | DTO、枚举、常量和跨端协议 |
| `DevNexus.Client.Shared` | Blazor 共享 UI、客户端状态和 API / SignalR 服务 |
| `DevNexus.Client.Web` | WebAssembly 客户端宿主 |
| `DevNexus.Client` | MAUI 桌面客户端宿主 |
| `DevNexus.AppHost` | Aspire 宿主，连接外部依赖并启动 API / Web 客户端 |

## 实时入口

- `/chat-hub`：聊天、队列、终端和运行态事件。
- `/artifact-hub`：Artifact 与文件解析状态事件。
- `/swarm-hub`：Swarm 工作包、Agent 状态和控制事件。

## 数据与运行时

- PostgreSQL 保存业务数据、Identity、Provider、文件资产、会话和审计数据。
- Redis 用于缓存和 SignalR 背板。
- Qdrant 承载向量检索。
- Elasticsearch 承载会话与消息搜索。
- Seq 与 OpenTelemetry 承载结构化日志和观测数据。
- Hangfire 运行后台任务。

## 推荐入口

- 运行入口：[安装指南](../00-getting-started/installation.md)
- 功能清单：[功能特性](./features.md)
- 架构入口：[架构索引](../02-architecture/README.md)
- 接口入口：[API 规格说明](../04-api/api-specification.md)
