# Agent Runtime 架构

## 1. 文档定位

本文档描述 DevNexus AI 当前已落地的 Agent Runtime 主链架构。

本文档只记录当前仓库中已经存在并可验证的运行时实现。

![Agent 运行时状态机](../assets/Agent_Lifecycle_State_Machine.png)

## 2. 当前目标与结果

当前 Agent Runtime 已完成以下主链收敛：

1. 工具执行结果已统一为结构化分类与统一文本协议，不再由上层直接解析零散错误文案。
2. Agent Loop、挂起交互、会话运行态、CLI 运行态、队列态、前端展示态已经收口进统一运行时模型。
3. `PendingInteraction` 已成为正式持久化运行时实体，支持创建、解决、过期和前端实时同步。
4. `Credential Runtime` 已具备显式状态、失败计数、冷却窗口和前端展示基础。
5. `HostService` 仅保留内部结构化接口，模型工具边界由专门插件适配层承担。
6. 运行时关键链路已补入统一结构化事件流，前端可按事件刷新会话运行态。

## 3. 分层边界

### 3.1 Shared

Shared 负责定义跨端稳定协议：

1. `ChatSessionRunState`
2. `PendingInteractionKind`
3. `PendingInteractionStatus`
4. `ToolFailureReason`
5. `ToolSuggestedAction`
6. `CredentialRuntimeStatus`
7. `ServerEventType`
8. 运行时 DTO（如 `ChatSessionRuntimeDto`、`PendingInteractionDto`、`ServerEvent`）

### 3.2 Domain

Domain 负责承载运行时事实源实体与仓储抽象：

1. `PendingInteraction`
2. `UserIntegration`
3. `ChatSession`
4. `ChatMessage`
5. `IPendingInteractionRepository`
6. `IRuntimeEventNotifier`

### 3.3 Core

Core 负责运行时编排与状态解析：

1. `ChatSessionRuntimeResolver`
2. `ChatSessionRuntimeInspector`
3. `PendingInteractionService`
4. `PendingInteractionDefinitionBuilder`
5. `CredentialRuntimeStatusResolver`
6. `ChatSessionRuntimeService`
7. `SwarmSessionControlService`

### 3.4 Infrastructure / ApiService

Infrastructure 与 ApiService 负责外部接入与事件发射：

1. `HostService`
2. `HostTextPlugin`
3. `SwarmHostPlugin`
4. `RuntimeEventNotifier`
5. `ToolInvocationNotifier`
6. `PendingInteractionExpirationService`
7. `SignalR Hub / Notifier / Repository`

### 3.5 Client

Client 只消费结构化状态，不承担业务裁决：

1. `ChatState`
2. `SignalRService`
3. `ChatContainer`
4. `PendingInteractionPanel`
5. `ChatSessionRunStateDisplay`

## 4. Tool Runtime

### 4.1 统一结果协议

工具执行主链已统一到：

1. `TaggedExecutionOutput`
2. `TaggedExecutionText`
3. `ToolExecutionResultClassifier`

内部宿主执行已统一到：

1. `IHostStructuredService`
2. `HostOperationResult`
3. `HostCommandExecutionResult`
4. `HostTextOperationResult`
5. `HostFileListOperationResult`

### 4.2 当前规则

1. 内部服务只消费结构化宿主结果。
2. 文本协议只保留在 `HostTextPlugin` / `SwarmHostPlugin` 边界。
3. `HostService` 本体不再直接暴露模型工具函数。

## 5. Decision Runtime

### 5.1 Agent Loop 裁决

当前 Agent Loop 决策链为：

1. 工具输出经 `ToolExecutionCollectorFilter` 采集
2. `ToolExecutionResultClassifier` 生成结构化失败语义
3. `LlmResponseEvaluator` / `RepairContextBuilder` 提供评估上下文
4. `ChatAgentLoopCoordinator` 根据结构化结果决定继续、停止或创建挂起交互

### 5.2 当前约束

1. 一旦工具结果标记 `RequiresHumanIntervention`，当前自动修复链必须停止。
2. 需要补参/审批时，必须转为 `PendingInteraction`，不得继续自动重试。
3. 内部修复提示不会进入用户可见消息主流。

## 6. Pending Interaction Runtime

### 6.1 运行时实体

`PendingInteraction` 当前具备：

1. `Kind`
2. `Status`
3. `Title`
4. `Description`
5. `RequestedData`
6. `ResolutionData`
7. `ExpiresAt`
8. `RetryToken`
9. `SourceTool`

### 6.2 当前行为

1. 同会话、同来源工具、同类交互会优先复用活跃项。
2. 创建后会立即推送结构化运行时事件，前端据此回拉最新挂起交互列表。
3. 解决后会推送最新挂起列表，并根据状态推送 `PendingInteractionResolved`。
4. 后台服务会周期性将过期项标记为 `Expired`，并推送 `PendingInteractionExpired`。

## 7. Credential Runtime

### 7.1 当前模型

当前凭证运行时基于 `UserIntegration` 实体上的以下字段治理：

1. `TokenExpiresAt`
2. `LastCredentialRefreshAt`
3. `ConsecutiveAuthFailureCount`
4. `LastAuthFailureAt`
5. `CooldownUntil`
6. `ValidationStatus`
7. `ValidationError`

### 7.2 当前状态集合

`CredentialRuntimeStatus` 当前包含：

1. `Ready`
2. `ExpiringSoon`
3. `Expired`
4. `Invalid`
5. `Inactive`
6. `CoolingDown`
7. `Unknown`

### 7.3 当前治理规则

1. 验证失败且命中认证错误特征时，递增连续失败计数。
2. 连续失败达到阈值后进入冷却期。
3. 冷却期内不再继续自动读取该凭证。
4. 验证成功后清空失败计数、失败时间和冷却窗口，并记录最近刷新时间。

## 8. Session Runtime

### 8.1 统一会话运行态

当前会话主运行态统一为 `ChatSessionRunState`，包含：

1. `Idle`
2. `Generating`
3. `Queued`
4. `Running`
5. `WaitingForInput`
6. `WaitingForPendingInput`
7. `WaitingForApproval`
8. `Recovering`

### 8.2 当前实现

1. `ChatSessionRuntimeResolver` 是后端唯一运行态解析器。
2. `ChatSessionRuntimeInspector` 负责统一聚合挂起交互、CLI、排队与消息状态。
3. `ChatSessionRuntimeService` 负责将解析结果映射到会话快照 DTO。
4. `ChatQueueService` 直接消费统一 runtime snapshot，不再额外包一层状态解析器。
5. `ChatState` 主路径优先使用服务端 runtime 快照。
6. 客户端本地只保留 `SetSessionGeneratingOptimistic(...)` 作为短时 optimistic 生成态。

## 9. Frontend State Runtime

### 9.1 当前原则

前端主路径不再从消息正文或布尔值猜测运行态。

当前前端运行态消费来源为：

1. `/api/v1/chat/sessions/{sessionId}/runtime`
2. `QueuedMessagesReceived`
3. `ServerEventReceived`

### 9.2 展示层统一

客户端运行态展示已统一到 `ChatSessionRunStateDisplay`：

1. 详细说明文案
2. 紧凑标签
3. 标题栏连接标签
4. 输入框占位文案
5. 会话列表样式类

## 10. 结构化事件流

### 10.1 当前事件通道

结构化运行时事件统一通过：

1. `RuntimeEventNotifier`
2. `ServerEvent`
3. `ServerEventReceived`

进行推送。

### 10.2 已接入事件

当前已显式发射：

1. `ToolInvocationStarted`
2. `ToolInvocationCompleted`
3. `ToolInvocationFailed`
4. `PendingInteractionCreated`
5. `PendingInteractionResolved`
6. `PendingInteractionExpired`
7. `GenerationStarted`
8. `GenerationCompleted`
9. `GenerationCancelled`
10. `GenerationFailed`
11. `SessionSuspended`
12. `SessionResumed`
13. `SessionCancelled`
14. `CliExec*` 统一终端运行时事件
15. `QueueStateChanged`
16. `SwarmSessionStarted`
17. `SwarmStarted`
18. `SwarmCompleted`
19. `SwarmFailed`
20. `SwarmCancelled`
21. `SwarmContextPackagesUpdated`
22. `SwarmAgentStatusChanged`
23. `SwarmControlCommand`
24. `SwarmConfirmationRequested`
25. `SwarmArbitrationEvent`

### 10.3 当前消费方式

客户端当前已订阅 `OnServerEvent`，并在聊天容器中对关键运行时事件触发 runtime 刷新。

## 11. 非主链机制

以下机制不属于当前 Agent Runtime 主链：

1. `IsGenerating / SetGenerating / IsSessionGenerating / GetGeneratingSessions`
2. `IHostService` 文本宿主接口
3. `HostService` 上直接暴露给模型的 `KernelFunction`
4. 工具主链对 `[SUCCESS] / [FAILURE] / [EXCEPTION]` 的分散字符串判断
5. `ChatSessionRuntimeDto` 中未被消费的冗余布尔和计数字段
6. 客户端本地 `PendingQueueCount` 冗余状态
7. 前端运行态展示的重复文案 switch 主路径
8. 聊天与 Swarm 侧依赖独立 SignalR 方法名表达状态的旧事件主链

## 12. 当前边界

仓库内可能存在以下类型的“兼容”字样，但它们不属于 Agent Runtime 主链：

1. 第三方库或静态资源
2. 非 runtime 的业务模块说明
3. Provider / S3 / OpenAI-compatible 等产品语义

这些内容不属于本架构文档的运行时范围。
