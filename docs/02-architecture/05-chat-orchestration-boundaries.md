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

## 删除补偿边界

数据库事务只覆盖会话拥有的主数据。运行时任务取消、会话记忆删除和搜索索引清理属于事务外补偿步骤。

## 维护规则

- 消息状态裁决先于最终持久化。
- thinking 合并规则只保留在 `ChatThinkingPersistenceCoordinator`。
- 成功完成后的索引和追踪副作用由 `ChatMessageCompletionCoordinator` 处理。
- 主数据删除和外部补偿必须保持分离。
