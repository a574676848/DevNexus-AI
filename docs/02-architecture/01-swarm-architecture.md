# Swarm 上下文工作包架构（代码实况）

## 1. 文档范围

本文档仅描述当前仓库中可直接验证的 Swarm 架构实现，不保留历史方案或阶段设计。

![Swarm 多智能体协作时序](../assets/Sequence_Diagram.png)

权威事实源：

- `src/backend/DevNexus.Core/Services/Swarm/*`
- `src/backend/DevNexus.ApiService/Hubs/SwarmHub.cs`
- `src/backend/DevNexus.ApiService/Services/SwarmEventService.cs`
- `src/common/DevNexus.Shared/Enums/SwarmEnums.cs`

## 2. 分层边界

### 2.1 API / Hub 层

- `SwarmController` 提供会话查询与中止 REST 能力：
  - `GET /api/v1/swarm/sessions`
  - `POST /api/v1/swarm/sessions/{sessionId}/abort`
- `SwarmHub` 提供会话加入、暂停、恢复、中止与确认回传。

### 2.2 Core 编排层

- `ContextDrivenSwarmOrchestrator`：总入口，负责上下文分析、工作包切分、执行编排与结果汇总。
- `SwarmPackagePlanner` / `SwarmPackageScheduler`：负责工作包规划、就绪判定与执行调度。
- `ContextBoundaryAnalyzer`、`WorkPackageDecomposer`：负责围绕上下文边界拆分工作包，而不是按角色拆分。
- `ContextCapsuleBuilder`、`ContextBroadcastService`：负责上下文胶囊与共享信息广播。

### 2.3 Core 协同子系统

- `SwarmTaskExecutor`：围绕上下文工作包执行 Agent 或群聊协同。
- `StructuredHandoffService`：在工作包之间执行结构化交接。
- `GroupChatCoordinator`：处理单个工作包内的多智能体讨论。
- `IResponseEvaluator` 及实现：统一评估执行结果质量。
- `SwarmSessionControlService` / `SwarmSessionViewService`：收口会话控制与视图快照查询。

### 2.4 持久化与状态

- 会话与工作包实体：`ContextSwarmSession`、`ContextWorkPackageRecord`。
- 会话运行控制：`SwarmSessionRegistry`（`Running/Paused/Aborted`）。
- 对外状态枚举：`SwarmStatus`、`SwarmTaskStatus`（当前仍作为工作包状态传输协议）。

## 3. 主执行流程

1. `ChatService` 触发 Swarm 升级。
2. `ContextDrivenSwarmOrchestrator.OrchestrateAsync` 接收请求并分析任务、状态、记忆、证据四类上下文。
3. 规划链路输出 `SwarmExecutionPlan`，内部包含一组具备完整上下文闭环的 `ContextWorkPackage`。
4. `SwarmPackageScheduler` 开始调度：
   - 注册会话状态
   - 落库初始会话与工作包
   - 广播工作包快照与上下文胶囊
   - 按依赖与策略调度 ready 工作包
5. 每个工作包经上下文装配、执行、结构化交接、评估与状态更新。
6. 会话完成/失败/中止后执行 finalize 与资源清理。

## 4. 执行策略（当前实现）

工作包调度不再暴露旧的“编排模式”中心概念，而是根据工作包特征选择执行策略。

当前已落地的主策略包括：

- 顺序执行
- 并发执行
- 群聊讨论
- Agent 直接执行

策略选择发生在工作包规划和调度阶段，而不是先生成一张独立的角色式任务图。

## 5. 状态机（当前枚举）

### 5.1 工作包状态 `SwarmTaskStatus`

- `Pending`
- `Ready`
- `InProgress`
- `Completed`
- `Failed`
- `Transferred`
- `GroupChatting`
- `Skipped`
- `Evaluating`
- `Retrying`

### 5.2 会话状态 `SwarmStatus`

- `Running`
- `Completed`
- `Failed`
- `Paused`
- `Aborted`

## 6. 实时通信契约

Hub 路径：`/swarm-hub`

### 6.1 客户端调用（C -> S）

- `ResolveConfirmation(confirmationId, approved)`
- `JoinSession(sessionId)`
- `PauseSession(sessionId)`
- `ResumeSession(sessionId)`
- `AbortSession(sessionId)`
- `LeaveSession(sessionId)`

### 6.2 服务端事件（S -> C）

由 `SwarmHub` 与 `SwarmEventService` 共同推送：

- `ServerEventReceived`

说明：

- `JoinSession` 后，服务端会给调用者补发当前工作包快照和缓存中的 Agent 状态，统一通过 `ServerEventReceived` 返回。
- `SwarmStarted` / `SwarmCompleted` / `SwarmFailed` / `SwarmCancelled` / `SwarmConfirmationRequested` 等事件类型由 `ServerEventType` 定义。

## 7. 控制与恢复

- 暂停/恢复/中止通过 `SwarmSessionRegistry` 驱动。
- 工作包调度循环会实时检查会话控制状态。
- 中止操作会触发取消令牌并更新会话状态。
- 会话级资源在 finalize 阶段统一清理。

## 8. 与聊天编排的关系

- Swarm 不替代普通聊天流式链路。
- 聊天是否升级为 Swarm，由复杂度评估与上下文拆分需求共同决定。
- Swarm 完成后结果回到聊天消息链路，保持统一会话体验。

## 9. 维护规则

- 增删 Hub 事件或方法时，先改代码与客户端订阅，再更新本文件。
- 新增执行策略或上下文协议字段时，必须同时更新：
  - 规划层模型
  - 事件契约
  - 本文档第 4 节和第 6 节
- 本文档只描述当前代码中的 Swarm 主链和维护规则。
