# DevNexus API 规格说明

## 1. 说明

本文档只保留当前代码可直接验证的结果信息，不保留历史过程说明。

- REST 与 SignalR 的最终事实源：
  - `src/backend/DevNexus.ApiService`
  - `src/common/DevNexus.Shared`
- 联调时优先使用 Swagger（`/swagger`）查看请求与响应模型。

## 2. REST 入口

常规业务接口使用 `/api/v1/*`。

更新系统使用独立入口：

- `/api/update`
- `/api/update/events`

## 3. 当前控制器清单（代码实况）

### 3.1 认证与用户

- `AuthController` - `api/v1/auth`
- `UserController` - `api/v1/user`
- `UserIntegrationController` - `api/v1/user/integrations`

### 3.2 聊天、语义与存储

- `ChatController` - `api/v1/chat`
- `ArtifactController` - `api/v1/artifact`
- `StorageController` - `api/v1/storage`
- `UploadController` - `api/v1/uploads`
- `FileAssetsController` - `api/v1/file-assets`
- `FileTasksController` - `api/v1/file-tasks`
- `MemoryController` - `api/v1/memory`
- `NoteController` - `api/v1/notes`

### 3.3 Provider 与模型能力

- `LLMProviderController` - `api/v1/providers/llm`
- `EmbeddingProviderController` - `api/v1/providers/embedding`
- `SearchProviderController` - `api/v1/providers/search`
- `StorageProviderController` - `api/v1/providers/storage`
- `NoteProviderController` - `api/v1/providers/note`
- `ModelPricingController` - `api/v1/model-pricing`
- `SkillController` - `api/v1/skill`

### 3.4 Swarm、系统与审计

- `SwarmController` - `api/v1/swarm`
- `SystemController` - `api/v1/system`
- `AuditAnalyticsController` - `api/v1/auditanalytics`

### 3.5 更新发布相关

- `UpdateManifestController` - `api/update`
- `UpdateClientEventsController` - `api/update/events`
- `AdminReleasesController` - `api/v1/admin/releases`
- `AdminRolloutsController` - `api/v1/admin/rollouts`
- `AdminUpdateObservabilityController` - `api/v1/admin/update-observability`

## 4. Auth 核心接口

`AuthController` 当前主要接口：

- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh-token`
- `POST /api/v1/auth/logout`
- `POST /api/v1/auth/logout-all`
- `POST /api/v1/auth/change-password`
- `GET /api/v1/auth/me`
- `PUT /api/v1/auth/profile`

## 5. SignalR 入口与方法

### 5.1 Hub 路径

- `/chat-hub`
- `/artifact-hub`
- `/swarm-hub`

### 5.2 ChatHub 客户端可调用方法（代码实况）

- `SendMessage`
- `CancelMessageGeneration`
- `CreateChatSession`
- `GetChatSessions`
- `GetChatMessages`
- `GetQueuedMessages`
- `CancelQueuedMessage`
- `ClearQueuedMessages`
- `SendCliInput`
- `TerminateCliSession`
- `GetCliExecSession`
- `PollCliExecSession`
- `GetCliExecLog`
- `WaitCliExecSession`
- `RollbackCliExecSession`

### 5.2.1 Chat REST 终端补充接口

- `GET /api/v1/chat/sessions/{sessionId}/active-terminals`
- `GET /api/v1/chat/sessions/{sessionId}/terminals/{recordId}/output`

### 5.3 ArtifactHub

- 当前只实现连接时自动入组（按用户组推送）。
- 无额外客户端可调用业务方法。

### 5.4 SwarmHub 客户端可调用方法

- `ResolveConfirmation`
- `JoinSession`
- `PauseSession`
- `ResumeSession`
- `AbortSession`
- `LeaveSession`

## 6. Provider 类型（与代码一致）

来源：`src/common/DevNexus.Shared/Enums/ProviderEnums.cs`

- `ProviderType`: `OpenAICompatible`, `Gemini`, `Kimi`, `MiniMax`, `DeepSeek`, `GLM`
- `EmbeddingProviderType`: `Doubao`, `OpenAI`, `Local`
- `SearchProviderType`: `SearXNG`, `Tavily`, `JinaReader`, `Firecrawl`
- `StorageProviderType`: `Local`, `AwsS3`, `AliyunOss`, `QiniuKodo`, `TencentCos`, `MinIO`, `CloudflareR2`, `S3Compatible`
- `NoteProviderType`: `Memos`, `Notion`, `Obsidian`, `Custom`

## 7. 鉴权约定

- 默认鉴权方式：JWT Bearer。
- 大多数控制器使用 `[Authorize]`。
- 管理接口使用 `AdminOnly` 策略或 `Admin` 角色限制。
- `UpdateClientEventsController` 允许匿名上报更新事件。

## 8. 维护规则

- 当控制器、Hub 方法、DTO、枚举变化时，先更新 Swagger 和代码，再更新本文件。
- 本文档只记录稳定接口和运行时契约。
