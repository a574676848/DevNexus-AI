# 聊天编排边界

本文档描述当前聊天执行管线的职责边界、状态顺序和补偿规则。

## 主运行流

### 标准流式聊天

1. `ChatService.StreamMessageAsync`
2. `GetOrCreateChatSessionAsync`
3. `CreateUserMessageAsync`
4. 创建 `Status = in_progress` 的 assistant 消息
5. `StreamAiResponseAsync`
6. `ChatStreamingPreparationService`
7. `StreamBlockParser` 增量解析输出块
8. `ToolBlockExecutionCoordinator` 执行搜索和网页读取块
9. `ChatAgentLoopCoordinator` 判断 `None` / `Stop` / `Retry`
10. 无需重试时进入 `ChatStreamingFinalizer`
11. `ChatMessageCompletionCoordinator`
12. `TriggerMemoryConsolidationCheckAsync`

### Swarm 升级

1. `ChatService.StreamMessageAsync`
2. 复杂度评估
3. 预写 `swarmMode = true` 的 assistant 占位消息
4. `ExecuteSwarmExecutionAsync`
5. `ThinkingAccumulatingChannelWriter`
6. `ISwarmOrchestrator`
7. `ContextDrivenSwarmOrchestrator`
8. `SwarmPackageScheduler`
9. `ChatSwarmFinalizer`
10. `ChatMessageCompletionCoordinator`

### 会话删除

1. `ChatService.DeleteChatSessionAsync`
2. 所有权校验
3. `ChatSessionDeletionCoordinator`
4. `ChatSessionCleanupCoordinator`
5. 事务内删除会话主数据：Artifact、Message、Session
6. 事务外补偿清理：会话记忆、搜索索引、运行时任务

## 协作者职责

| 协作者 | 职责 |
|--------|------|
| `ChatService` | 公开入口、主分支选择、会话和首批消息创建、协作者串联 |
| `ChatStreamingPreparationService` | Provider 解析、历史构建、生成前上下文提示 |
| `ChatAgentLoopCoordinator` | 生成后评估、停止或重试决策、修复消息创建 |
| `ChatStreamingFinalizer` | 正常、取消、错误和截断状态持久化 |
| `ChatSwarmFinalizer` | Swarm 成功、取消、失败状态持久化和事件发射 |
| `ChatMessageCompletionCoordinator` | 搜索索引、完成追踪、经验沉淀调度 |
| `ChatThinkingPersistenceCoordinator` | thinking/text partial 合并与最终清理 |
| `ChatGenerationCancellationRegistry` | 同一会话生成任务的取消令牌单飞注册、取消与完成移除 |
| `PendingGenerationCancelQueue` | 客户端断线期间的生成取消请求去重暂存与重连重放 |
| `SwarmAgentStatusStore` | Swarm 智能体状态快照的单一来源，供事件服务写入、Hub 重连补偿读取 |
| `ChatSessionCleanupCoordinator` | 删除前运行时清理 |
| `ChatSessionDeletionCoordinator` | 主数据事务删除与外部补偿顺序 |

## 消息状态

Assistant 消息使用以下状态：

- `in_progress`
- `completed`
- `truncated`
- `cancelled`
- `error`

状态规则：

1. `in_progress -> completed/truncated` 只发生在 Agent Loop 确认无需重试之后。
2. `in_progress -> cancelled` 对应用户取消。
3. `in_progress -> error` 对应不可恢复异常。
4. Swarm 最终消息复用同一组状态值。

## 消息内容契约

`ChatMessageDto.Content` 只承载可直接展示的正文，并与 `TextContent` 保持同义。思考内容只能写入 `ThinkingContent`，不得序列化到 `Content`，也不得在 DTO 内容中拼接标签。

后端持久化层的 `ChatMessage.Content` 是字典结构，协议键统一由 `ChatMessageContentKeys` 定义：`Text` 对应可见正文，`Thinking` 对应最终思维链，`TextPartial` / `ThinkingPartial` / `ThinkingExternalPartial` 仅用于生成期间的临时增量。核心 Chat/Swarm 路径不得直接散落 `"text"`、`"thinking"`、`"text_partial"`、`"thinking_partial"` 或 `"thinking_external_partial"` 字符串。

前端渲染和恢复统一读取结构化字段：正文来自 `TextContent` / `Content`，思考步骤来自 `ThinkingContent`。历史回放、取消固化、错误固化和 Swarm 结果都必须遵守同一契约，避免重连或回填时污染正文。

## 删除补偿边界

数据库事务只覆盖会话拥有的主数据。运行时任务取消、会话记忆删除和搜索索引清理属于事务外补偿步骤。

## 维护规则

- 消息状态裁决先于最终持久化。
- 同一会话同一时刻只能注册一个生成取消令牌；完成清理必须只移除本次注册的令牌，避免旧生成流误删新生成流。
- 客户端取消生成时若 ChatHub 断线，取消请求必须按会话去重暂存，并在 ChatHub 重连后补发；前端不应只记录日志后丢弃取消意图。
- ChatHub 断线后重连必须区分初次连接和恢复连接；恢复连接要刷新 runtime，若当前会话仍在生成态则重载会话消息和流式块状态。
- ChatHub 触发队列续跑时必须传递当前连接取消令牌，断线或取消后不得用 `CancellationToken.None` 继续抢占队列消息。
- thinking 合并规则只保留在 `ChatThinkingPersistenceCoordinator`。
- DTO 内容不得承载 thinking 标签；任何新增路径都必须写入 `TextContent` / `ThinkingContent`。
- Swarm 复杂度评估失败必须使用显式 fallback 标记和临界降级向量，不得伪装成低复杂度任务。
- Swarm 智能体状态只能由 `SwarmAgentStatusStore` 持有；Hub 不缓存业务状态，事件服务也不得调用 Hub 静态成员。
- 成功完成后的索引和追踪副作用由 `ChatMessageCompletionCoordinator` 处理。
- 主数据删除和外部补偿必须保持分离。
