# DevNexus AI 功能特性

![统一提供商与扩展矩阵](../assets/Provider_Plugin_Matrix.png)

本文档只列出当前代码中可以直接验证的功能。

## 聊天与实时生成

- 会话创建、会话列表、消息历史和消息删除。
- ChatHub 流式输出，支持生成取消、排队消息、终端输入和 CLI 执行状态轮询。
- Artifact 分屏预览，支持文本、代码、图表和文件任务结果块。
- Pending Interaction 支持人工确认回环。

## Provider 管理

- LLM Provider：`OpenAICompatible`、`Gemini`、`Kimi`、`MiniMax`、`DeepSeek`、`GLM`。
- Embedding Provider：`Doubao`、`OpenAI`、`Local`。
- Search Provider：`SearXNG`、`Tavily`、`JinaReader`、`Firecrawl`。
- Storage Provider：`Local`、`AwsS3`、`AliyunOss`、`QiniuKodo`、`TencentCos`、`MinIO`、`CloudflareR2`、`S3Compatible`。
Provider 密钥通过 `IEncryptionService` 加密存储，运行时由对应 Factory 或管理服务解析。

## 文档语义摄取

- 上传文件可生成 `SmartDocument` 语义派生物。
- 文本、代码、Office/PDF、图片分别由对应解析器处理。
- Qdrant 与 Kernel Memory 参与语义检索链路。
- 解析失败原因通过 Artifact / 文件状态回传。

## 文件资产与任务

- `UploadSession` 管理上传生命周期。
- `FileAsset` 和 `FileVersion` 保存原始文件与结果文件事实。
- `FileTask` 管理真实文件处理任务。
- 任务工作区内的 `runner.ps1` 和 `runner.py` 承担格式特化处理。
- `outputs` 目录中的有效结果会回灌为新文件资产。

## Swarm 协作

- 复杂任务可拆分为上下文工作包。
- 工作包支持顺序、并行和协作式执行策略。
- SwarmHub 推送工作包状态、Agent 状态和确认请求。
- 会话支持暂停、恢复、中止和崩溃恢复。

## 客户端体验

- Web 与桌面客户端共享 `DevNexus.Client.Shared`。
- 登录、聊天、设置、Provider 管理、技能管理、发布中心、审计看板和系统信息页均在共享组件中实现。
- 客户端通过 REST 获取数据，通过 SignalR 接收实时状态。
