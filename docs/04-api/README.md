# DevNexus AI API 文档

本目录说明当前仓库里仍在使用的 REST API 与 SignalR 入口。

## 文档

| 文档 | 说明 |
|------|------|
| `api-specification.md` | 当前 REST 与 SignalR 规格说明 |

## 当前接口主线

### 1. 认证与用户
- `AuthController`
- `UserController`
- `UserIntegrationController`

### 2. 聊天、记忆与语义解析
- `ChatController`
- `ArtifactController`
- `MemoryController`
- `StorageController`

### 3. 文件资产与任务
- `UploadController`
- `FileAssetsController`
- `FileTasksController`

### 4. Provider 与模型配置
- `LLMProviderController`
- `EmbeddingProviderController`
- `SearchProviderController`
- `StorageProviderController`
- `ModelPricingController`
- `SkillController`

### 5. Swarm、系统与审计
- `SwarmController`
- `SystemController`
- `AuditAnalyticsController`

### 6. 客户端更新与发布管理
- `UpdateManifestController`
- `AdminReleasesController`
- `AdminRolloutsController`
- `AdminUpdateObservabilityController`
- `UpdateClientEventsController`

## 实时入口

- Chat Hub: `/chat-hub`
- Artifact Hub: `/artifact-hub`
- Swarm Hub: `/swarm-hub`

## 调试建议

- 具体请求/响应以 Swagger 为准。
- 控制器增删和 DTO 变化以 `src/backend/DevNexus.ApiService` 与 `src/common/DevNexus.Shared` 为准。
