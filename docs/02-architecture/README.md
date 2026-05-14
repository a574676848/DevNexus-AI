# DevNexus AI 架构文档

本目录保留当前代码中可直接验证的架构说明。

## 文档索引

### 智能体编排

- [01-swarm-architecture.md](./01-swarm-architecture.md) — Swarm 会话、任务拆解、状态流和协作方式

### 语义模型

- [02-semantic-document-model-design.md](./02-semantic-document-model-design.md) — SmartDocument 的职责边界与原始文件资产的关系

### 语义摄取

- [03-semantic-document-ingestion.md](./03-semantic-document-ingestion.md) — 文件解析、文本提取和语义链路

### 搜索与知识获取

- [04-search-pipeline-and-repo-parser.md](./04-search-pipeline-and-repo-parser.md) — 联网搜索、网页摄取和仓库解析

### 聊天编排边界

- [05-chat-orchestration-boundaries.md](./05-chat-orchestration-boundaries.md) — ChatService 与协同组件的职责边界和状态流转

### Agent Runtime 架构

- [06-agent-runtime-architecture.md](./06-agent-runtime-architecture.md) — Agent Runtime 主链、运行时边界与事件模型

### CLI Runtime 稳定性架构

- [07-cli-runtime-stability-architecture.md](./07-cli-runtime-stability-architecture.md) — CLI Runtime 分层边界与稳定性收口

### 用户与认证

- [08-user-auth.md](./08-user-auth.md) — 认证分层边界
- [09-auth-token-model.md](./09-auth-token-model.md) — 访问令牌与刷新令牌模型

### AI Agent 优化

- [10-ai-agent-optimization-roadmap.md](./10-ai-agent-optimization-roadmap.md) — Token 缓存、工具调用成功率、上下文压缩和可观测性演进路线

## 边界说明

- SmartDocument 是语义派生物，不是原始文件事实源
- 真实文件处理能力以 `UploadController`、`FileAssetsController`、`FileTasksController` 及相关服务为准
- 具体实现细节优先参考 `src/backend` 与 `src/client` 中的代码
