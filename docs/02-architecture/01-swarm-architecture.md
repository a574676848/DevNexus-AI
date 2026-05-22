# Swarm 上下文工作包架构

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
- `SwarmControlCommandBuilder`：收口暂停、继续、中止和工作包重试控制事件载荷，确保控制命令也携带当前 `StatusSummary`。
- `SwarmSessionFinalizationPolicy`：收口取消、异常和未完成工作包的会话收尾语义。
- `SwarmPackageCancellationPolicy`：收口工作包调度取消语义，取消信号出现后不再启动新工作包，并将未完成工作包标记为 `Aborted`。
- `SwarmHostPlugin`：保留工作包 VFS 优先读写能力，并通过 `ICliRuntimeCoordinator` 暴露 `WaitCommandAsync` / `SendCommandInputAsync` / `StopCommandAsync`，确保 Swarm 工作包能续接或停止长时间运行的 CLI 会话。

### 2.4 持久化与状态

- 会话与工作包实体：`ContextSwarmSession`、`ContextWorkPackageRecord`。
- 会话运行控制：`SwarmSessionRegistry`（`Running/Paused/Aborted`），同时维护会话取消令牌和工作包重试单飞占位。
- 对外状态枚举：`SwarmStatus`、`SwarmTaskStatus`（当前仍作为工作包状态传输协议）。
- 对外状态摘要：`SwarmSessionStatusSummaryBuilder` 基于工作包快照和暂停态生成 `StatusSummary`，供 REST、Hub 初始快照和实时事件复用。
- 控制命令摘要：`SwarmControlCommandDto` 使用 `Command + Accepted + Message + StatusSummary` 描述暂停、继续、中止和工作包重试后的可观察状态，客户端不再只通过命令字符串推断主状态。
- 会话收尾策略：用户主动取消时标记会话 `Aborted`，并将未完成的持久化工作包标记为 `Skipped`；运行中取消令牌传入调度器时，内存执行计划中的未完成工作包标记为 `Aborted`；异常时标记会话 `Failed` 并给未完成工作包补失败原因。
- 启动恢复策略：`SwarmSessionRecoveryService` 扫描异常中断会话后复用 `SwarmSessionFinalizationPolicy`，会话与未完成工作包必须一起进入终态。

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
- `SwarmContextPackagesUpdated` 的 `Data` 使用 `ContextSwarmPackageSnapshotDto`，同时包含 `Packages`、`PackageCount` 和 `StatusSummary`。
- `SwarmControlCommand` 的 `Data` 使用 `SwarmControlCommandDto`，同时包含 `Command`、`Accepted`、`Message` 和控制命令处理后的 `StatusSummary`。
- `SwarmStarted` / `SwarmCompleted` / `SwarmFailed` / `SwarmCancelled` / `SwarmConfirmationRequested` 等事件类型由 `ServerEventType` 定义。

### 6.3 状态摘要原则

- Core 层负责裁决会话主状态、说明文案和阶段计数。
- API / Hub 只转发结构化摘要，不重新推导阶段语义。
- Client 优先展示 `StatusSummary`，仅在旧载荷缺失摘要时用本地逻辑兜底。
- 失败优先于暂停、执行、评估和收尾展示，确保用户先看到需要处理的问题。

## 7. 控制与恢复

- 暂停/恢复/中止通过 `SwarmSessionRegistry` 驱动。
- 暂停/恢复/中止/重试必须通过 `SwarmControlCommandBuilder` 生成控制事件载荷，保证按钮反馈、事件流和状态摘要共享同一事实。
- 暂停/恢复必须先通过 `SwarmControlCommandPolicy` 判断会话是否仍可控制；已完成、失败或中止的终态会话必须返回 `Accepted=false` 的拒绝事件，不能伪装成已继续或已暂停。
- 工作包重试必须先通过 `SwarmControlCommandPolicy` 判断会话和工作包状态；会话缺失、已中止、已完成、工作包缺失或非失败工作包必须返回 `Accepted=false` 的 `RetryRejected`，不得通过异常打断 Hub/UI 状态闭环。
- 工作包重试单飞占位必须随会话中止或注销清理，避免长生命周期服务中陈旧占位导致后续同包重试被误拒。
- 暂停会话时必须同步持久化 `SwarmStatus.Paused`，恢复会话时必须同步持久化 `SwarmStatus.Running`，避免刷新或重连后只看到内存控制态。
- 工作包调度循环会实时检查会话控制状态。
- 中止操作会触发取消令牌，并通过 `SwarmSessionFinalizationPolicy.BuildUserAbort` 同步持久化会话状态与未完成工作包终态。
- 会话级资源在 finalize 阶段统一清理。
- `ContextDrivenSwarmOrchestrator` 捕获取消和异常后，必须通过 `SwarmSessionFinalizationPolicy` 持久化最终状态并推送 `SwarmCancelled` 或 `SwarmFailed`。
- `SwarmPackageScheduler` 收到取消信号后不得继续派发新的顺序包或并行包；已进入执行中的工作包收到取消时必须标记为 `Aborted`，而不是落入普通失败分支。
- 已完成工作包在取消或异常收尾时保持完成态；用户主动中止的未完成持久化工作包必须进入 `Skipped`，调度器取消中的未完成内存工作包必须进入 `Aborted`，异常恢复中的未完成持久化工作包必须进入 `Failed`，并补齐原因和结果摘要。
- `SwarmSessionRecoveryService` 处理服务重启前残留的 `Running` 会话时，也必须通过 `SwarmSessionFinalizationPolicy` 补齐未完成工作包终态，不能只更新会话状态。
- 工作包记录的 `StartedAt`、`CompletedAt` 和 `UpdatedAt` 由 `SwarmPackageRecordLifecyclePolicy` 统一维护。`Completed`、`Failed`、`Skipped` 和 `Transferred` 必须补齐 `CompletedAt`；工作包重试重新进入执行态时必须清空旧 `CompletedAt`。

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
- 修改 Swarm 主状态展示语义时，必须先更新 `SwarmSessionStatusSummaryBuilder` 及其测试，再同步前端兜底逻辑。
- 修改 Swarm 控制命令载荷时，必须先更新 `SwarmControlCommandBuilder` / `SwarmControlCommandPolicy` 及其测试，再同步 Hub 事件与客户端订阅。
- 修改 Swarm 会话注册表运行时资源语义时，必须更新 `SwarmSessionRegistry` 及其测试，确保取消令牌、控制态和工作包重试占位随会话生命周期闭环释放。
- 修改 Swarm 取消、失败或异常恢复语义时，必须先更新 `SwarmSessionFinalizationPolicy` 及其测试，再同步本文件第 7 节。
- 修改 Swarm 工作包记录生命周期语义时，必须先更新 `SwarmPackageRecordLifecyclePolicy` 及其测试，避免重试失败、跳过或重新执行后的时间字段不闭环。
- 修改 Swarm 工作包调度取消语义时，必须先更新 `SwarmPackageCancellationPolicy` / `SwarmPackageScheduler` 及其测试，确保取消后不会继续启动新工作包。
- 修改启动恢复语义时，必须确保 `SwarmSessionRecoveryService` 与运行中异常收尾共享同一策略，避免恢复后看板仍显示执行中工作包。
- 修改 Swarm Host 工具面时，必须同步验证 `SwarmHostPlugin` 与 `HostTextPlugin` 的 CLI 续接协议，避免普通 Agent Loop 与 Swarm 工作包对长命令等待、stdin 输入出现分叉。
- 本文档只描述当前代码中的 Swarm 主链和维护规则。
