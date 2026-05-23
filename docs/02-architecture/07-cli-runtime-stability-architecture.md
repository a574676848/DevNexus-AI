# CLI Runtime 稳定性架构

## 1. 文档定位

本文档描述当前仓库中已经存在的 CLI Runtime / Agent Loop 稳定性主链。

## 2. 当前主链

当前 CLI Runtime 已经形成以下闭环：

1. 命令执行前统一进入 `CliExecutionPolicyService` 做工作目录、危险命令、审批与路径策略判断。
2. `HostService` 负责把一次命令请求接入 CLI 主链，并创建审批事件、checkpoint 与终端输出通知。
3. `ProcessCliRuntimeHost` 负责持久化 shell、输出缓冲、运行态快照、超时清理和会话终止；命令完成哨兵协议由 Core 层 `CliCommandCompletionProtocol` 统一构造和解析。
4. `ICliRuntimeCoordinator` / `CliRuntimeCoordinator` 已作为统一协调入口，收口会话查询、日志分片、输入转发、等待终态、终止和回滚；stdin 输入由 Core 层 `CliRuntimeInputProtocol` 统一规范。
5. `ChatHub` 与 `ChatSessionRuntimeInspector` 已改为通过协调器消费 CLI 会话，而不是分别直连进程注册表与仓储。
6. `CliExecCheckpointService` 已覆盖高风险文件命令的快照创建、失效旧快照和显式回滚。
7. `ProcessCliRuntimeHost` 已具备可消费的 warm shell 预热与接管能力，`HostService` 创建会话前会先触发热池预热。
8. `ConfigurableCliSandboxSessionProvider` 已收口本地受限与容器隔离两类 sandbox，并具备 acquire / reuse / idle cleanup / orphan cleanup。
9. `TerminalOutputBuffer` / `TerminalStream` 的归档输出已经接入 CLI 日志读取主链，终端会话退出后仍可通过统一日志接口读取完整输出。
10. `AgentLoopRecoveryGuard` 已通过 `RuntimeRecoveryMiddleware`、`LoopGuardMiddleware` 和恢复管线把挂起交互拦截、不可恢复失败阻断与人工介入分流显式收口。
11. `AgentLoopExecutor` 已对 `ContextOverflow` 工具失败启用确定性恢复快路径，直接生成压缩上下文与分批读取指令，避免额外 LLM 评估放大上下文压力。
12. `CliRuntimeStatusSummaryBuilder` 已将等待输入、运行中、失败、超时、回滚等状态解释收口到 Core，客户端只消费摘要文案和动作语义。
13. `CliExecutionPolicyResult` 已提供稳定 `DecisionCode`，审批、阻断、重复命令循环等原因不依赖中文文案解析。
14. `PendingInteractionResolutionPolicy` 已将审批解决动作归一为稳定动作、CLI 授权范围和恢复消息，API 与 Agent Loop 不再手写解析审批字符串。

## 3. 分层落点

### 3.1 Shared

Shared 当前承载稳定协议：

1. `CliExecStatus`
2. `CliApprovalStatus`
3. `CliSessionMode`
4. `CliExecSessionDto`
5. `CliExecLogChunkDto`
6. `CliExecApprovalRequestDto`
7. `CliExecCheckpointDto`
8. `CliRuntimeStatusSummaryDto`

### 3.2 Domain

Domain 当前承载事实源：

1. `CliExecSession`
2. `CliExecCheckpoint`
3. `ICliExecSessionRepository`
4. `ICliExecCheckpointRepository`

### 3.3 Core

Core 当前承载稳定抽象与运行时编排：

1. `ICliRuntimeCoordinator`
2. `ICliProcessService`
3. `ICliExecService`
4. `ICliSandboxWarmPool`
5. `CliExecutionPolicyService`
6. `IChatSessionRuntimeInspector`
7. `RuntimeRecoveryMiddleware`
8. `LoopGuardMiddleware`
9. `CliCommandCompletionProtocol`
10. `CliRuntimeStatusSummaryBuilder`
11. `CliRuntimeInputProtocol`
12. `CliTerminationResultBuilder`
13. `CliRollbackResultBuilder`
14. `CliExecutionPolicyDecisionCode`
15. `PendingInteractionResolutionPolicy`

### 3.4 Infrastructure

Infrastructure 当前负责外部执行细节：

1. `CliRuntimeCoordinator`
2. `ProcessCliRuntimeHost`
3. `CliRuntimeWarmPool`
4. `ConfigurableCliSandboxSessionProvider`
5. `LocalRestrictedSandboxSessionProvider`
6. `ContainerSandboxSessionProvider`
7. `CliExecCheckpointService`
8. `CliOutputTextSanitizer` 复用 Core 层 `TerminalOutputTextSanitizer`，负责 ANSI 清理、非文本控制字符剔除和等待输入输出识别
9. `CliSessionPersistenceMapper` 负责 CLI 运行态到持久化会话字段的纯映射
10. `CliSessionRuntimeState` 与 `WarmShellEntry` 承载宿主内部运行态数据，避免 `ProcessCliRuntimeHost` 继续堆叠私有状态类型

### 3.5 Client / Api

Client 与 Api 当前只消费统一状态和事件：

1. `ChatHub` 通过协调器暴露 `poll / wait / log / terminate / rollback`
2. `ChatContainer` 与终端面板消费统一运行态和日志结果
3. 统一事件流继续复用 `CliExecApprovalRequired`、`CliExecStarted`、`CliExecWaitingForInput`、`CliExecCompleted`、`CliExecFailed`、`CliExecCancelled`、`CliExecTimedOut`、`CliExecRolledBack`
4. 客户端终端摘要优先展示 `CliSessionStateDto.StatusSummary`，缺失时才使用历史本地推导兜底。

## 4. 运行能力

### 4.1 统一协议与会话视图

以下协议已经是显式公共类型，而不再只是运行时拼装结果：

1. `CliExecApprovalRequestDto`
2. `CliExecSessionDto`
3. `CliExecLogChunkDto`
4. `CliExecCheckpointDto`
5. `CliApprovalStatus`
6. `CliRuntimeStatusSummaryDto`

同时，`CliExecStatus` 与 `CliSessionState` 已补齐 `Queued` / `RolledBack` 这类细化状态值。

### 4.1.1 运行态摘要

CLI 运行态摘要遵循以下规则：

1. Core 根据 `CliExecStatus`、`WaitingForInput` 和 `TerminationReason` 生成 `StatusSummary`。
2. `StatusSummary` 提供 `Tone`、`Label`、`Description`、`NextAction`、`RequiresInput`、`IsTerminal` 和终止原因文本。
3. 等待输入优先级最高，确保输入框明确切换为 stdin 语义。
4. 失败、超时、取消和回滚都给出稳定下一步动作，避免客户端重复推导恢复提示。
5. Api 层 `CliExecutionRuntimeMapper` 只负责从历史 metadata 恢复 DTO，不重新定义状态语义。

### 4.1.2 运行时输入协议

CLI stdin 输入遵循以下规则：

1. `CliRuntimeInputProtocol` 负责移除末尾换行，避免前端、聊天输入框或 SignalR 入口额外制造空回车。
2. 空输入仍是合法 stdin 空行，不被当成无效请求丢弃。
3. 协议同时生成模型可见的单行输入预览，用于运行态与日志链路描述输入动作。
4. `CliRuntimeCoordinator.WriteInputAsync(...)` 是唯一写入协调入口，Api 层只负责触发协调器，不重复广播同一状态事件。

### 4.1.3 执行策略裁决

CLI 执行策略遵循以下规则：

1. `CliExecutionPolicyService` 负责输出 `Allowed`、`DecisionCode`、`FailureReason`、`SuggestedAction`、审批指纹和命令模式。
2. `DecisionCode` 是审批、阻断和循环保护的稳定事实，Host、Agent Loop、UI 和测试不得通过解析中文 `Message` 推断策略原因。
3. 需要人工审批的命令必须携带 `CommandFingerprint` 与 `CommandPattern`，用于本次授权和同类命令授权。
4. `Message` 只作为中文友好展示文案，不作为策略分支条件。
5. 挂起交互解决动作由 `PendingInteractionResolutionPolicy` 统一归一：`approve` 兼容为 `approve-once`，`approve-pattern` 映射为会话内同类命令授权，`deny` 不写入授权，`submit` 用于补充信息。
6. `PendingInteractionResolutionResponse` 返回归一后的 `Action` 与 `ApprovalScope`，后续恢复执行不需要解析中文 `ResumeMessage`。
7. `PendingInteraction.ResolutionData` 与恢复消息 metadata 都写入 `resolutionAction` 和可选 `approvalScope`，确保审批通过后 Agent Loop 能按恢复上下文继续，而不是把恢复消息当普通聊天。

### 4.2 统一协调入口

CLI 主链已经显式拆成 `exec / process` 两层契约：

1. `ICliExecService` 负责接收执行请求
2. `ICliProcessService` / `ICliRuntimeCoordinator` 负责围绕既有会话做状态、日志、输入、终止和回滚
3. `HostTextPlugin` / `SwarmHostPlugin` 负责把模型可调用的 `WaitCommandAsync` / `SendCommandInputAsync` / `StopCommandAsync` 映射到协调器，不直接操作进程注册表

其中 `ICliRuntimeCoordinator` 当前承接以下 process 能力：

1. 会话状态查询
2. 日志分片读取
3. 等待终态
4. 输入转发
5. 终止
6. 回滚
7. 最近 checkpoint 查询

模型可见的 CLI 续接只允许通过 `HostTextPlugin` / `SwarmHostPlugin` 的显式工具函数进入协调器。`WaitCommandAsync` 用于继续等待或轮询当前聊天会话的终端输出，`SendCommandInputAsync` 用于向同一会话发送 stdin，`StopCommandAsync` 用于停止当前会话中卡住或不再需要的命令；这些工具都复用 `StatusSummary` 和输出尾部生成低噪文本协议，并输出 `recommendedTool` 稳定字段，避免模型在等待、stdin 和停止之间选错下一步工具。

`WaitCommandAsync` 的等待预算由 Core 层 `CliContinuationWaitBudgetPolicy` 统一管理，当前默认值为 10 秒、下限为 1 秒、上限为 30 秒。普通 Agent Loop 的 `HostTextPlugin` 与 Swarm 工作包的 `SwarmHostPlugin` 必须复用同一策略，避免两条工具面出现不同等待行为。

`SendCommandInputAsync` 只允许续接已有活跃会话。协调器必须先读取当前会话状态，再写入 stdin；未知会话返回 `[FAILURE]`，终态会话返回终态快照供模型总结，不得为了兜底而创建新的 `Queued` / `Running` 会话。

`StopCommandAsync` 缺失会话时也必须返回 `[FAILURE]`，并把 `recommendedTool` 置为 `ReviewResult`。已知终态会话可以返回成功态快照供模型总结；未知会话不能被模型误判为已经成功停止了某个活跃命令。

`recommendedTool` 遵循以下规则：

1. 会话仍在运行且未等待输入时推荐 `HostService.WaitCommandAsync`。
2. 会话等待输入或 `nextAction=SendInput` 时推荐 `HostService.SendCommandInputAsync`。
3. 终态会话推荐 `ReviewResult`，模型应总结或检查结果，不应继续调用 CLI 续接工具。
4. 停止命令返回后若会话仍活跃，推荐 `HostService.StopCommandAsync`，否则推荐 `ReviewResult`。

工具结果分类也复用同一协议边界：`[INFO]` 且带 `waitingForInput: true`、`nextAction: SendInput`、`recommendedTool: HostService.SendCommandInputAsync` 或“等待输入”的输出必须归类为 `PromptUserInput`，并引导调用 `HostService.SendCommandInputAsync`；`recommendedTool: HostService.WaitCommandAsync` 必须归类为 `WaitForCompletion`；停止命令返回 `[FAILURE]` 但仍推荐 `HostService.StopCommandAsync` 时必须归类为 `StopCommand`，继续停止同一终端会话。不得把等待输入误判为普通成功，也不得把停止未闭环误判为普通降级。CLI 等待 stdin 不创建产品化挂起交互，除非底层工具明确标记 `RequiresHumanIntervention`，否则 Agent Loop 必须继续使用同一终端会话续接。

### 4.3 Sandbox 生命周期

当前 sandbox 生命周期已具备：

1. lazy acquire
2. 同会话复用
3. 空闲清理
4. 孤儿租约清理
5. warm shell 预热
6. 热 shell 被真实会话接管后继续沿用原有锁与回收语义

### 4.4 Agent Loop 恢复链

当前恢复链已经显式分层：

1. `ToolExecutionRecordNormalizer` 负责归一工具结果
2. `RuntimeRecoveryMiddleware` 负责挂起交互和人工介入分流
3. `LoopGuardMiddleware` 负责连续不可恢复失败阻断
4. `ContextOverflowRepairPromptBuilder` 负责上下文溢出时的固定修复指令
5. `AgentLoopRecoveryPipeline` 负责按顺序执行恢复中间件

### 4.5 输出治理

当前输出治理已接入统一日志事实源：

1. 运行中优先读取进程内存缓冲
2. 终端结束后通过 `TerminalOutputBuffer` / `TerminalStream` 读取归档输出
3. `CliExecSession` 已持久化最近关联的 `TerminalStreamId`
4. `GetLog / Poll / Wait` 会优先复用统一会话视图与归档事实，而不是只依赖进程内存缓冲
5. `TerminalOutputPreviewBuilder` 负责 CLI 工具调用返回给模型前的终端输出预览，保留总字符数、总行数、头部和尾部，并同时限制单行长度与行窗口，避免长输出或单行流水日志把后续上下文挤满
6. 终端完整输出仍以 `TerminalOutputBuffer` 归档为事实源，预览压缩只影响模型可见摘要，不替代日志读取和 UI 终端主视图
7. `CliCommandCompletionProtocol` 负责包装命令、生成完成哨兵并从输出中解析干净输出和退出码，避免 `ProcessCliRuntimeHost` 内继续堆叠平台分支与字符串协议。
8. `CliCommandCompletionProtocol` 必须先保存原始命令退出码，再输出完成哨兵，避免 `echo` 覆盖 `$?` / `$LASTEXITCODE` 导致失败命令被误判为成功；PowerShell 中 `$LASTEXITCODE` 为空时必须回退到 `$?`，覆盖 cmdlet 和表达式失败场景。
9. `CliRuntimeStatusSummaryBuilder` 负责模型和 UI 共同可见的低噪状态说明，避免运行中、等待输入、失败恢复在 Core、Api 和 Client 多处散落。
10. `CliRuntimeInputProtocol` 负责 stdin 输入规范化和单行预览，避免 Api、Client 与进程宿主各自处理换行、空行和长输入摘要。
11. `CliTerminationResultBuilder` 负责构建会话缺失、已结束和用户终止后的稳定结果、取消态摘要和持久化事实，避免 Infrastructure 协调器手写终止文案和状态语义。
12. `CliRollbackResultBuilder` 负责构建运行中阻断、回滚成功结果、回滚态摘要和持久化事实；回滚事件与 SignalR 返回值都携带统一 `State`，客户端不需要等待下一轮轮询才能更新终端主状态。
13. `CliCommandExecutionResult` 负责表达一次命令等待的结构化结果；等待预算耗尽但 shell 仍存活时返回 `StillRunning`，不把长命令误标记为失败或超时终态。
14. `TerminalOutputWatchSummaryBuilder` 负责识别错误、警告、审批拦截和交互输入提示，并合并去重观察摘要；`TerminalOutputBuffer` 只负责缓冲、刷新和持久化。
15. `TerminalArchivedOutputPreview` 负责裁剪数据库中的终端输出预览字段，只保留最近输出并标记较早内容已归档；完整终端日志仍以归档文件和日志读取接口为事实源。
16. `TerminalRetainedOutputSlice` 负责从进程内保留缓冲中读取增量输出；当旧偏移已经落在内存水位裁剪掉的历史之前时，必须返回当前保留窗口和裁剪标记，避免长命令续接或 `StillRunning` 结果丢失最新尾部事实。
17. `TerminalLogChunkOutputSlice` 负责区分日志分块的 live 与 archived 来源：live 输出已经由进程注册表按 `startIndex` 切片，协调器不得二次切片；archived 输出是完整事实源，仍由 Core 规则按 `startIndex` 返回增量。

### 4.5.1 命令等待状态

终端命令等待借鉴 openclacky 的 marker / idle / timeout 分层语义，但落到 DevNexus 的本地工作站定位后只保留产品化状态：

1. `Completed`：命令哨兵命中且退出码为 0。
2. `Failed`：命令哨兵命中但退出码非 0。
3. `StillRunning`：本次等待预算耗尽，命令仍在持久化 shell 内运行。
4. `Cancelled`：用户或上游取消令牌触发终止。
5. `ProcessUnavailable`：底层 shell 缺失或已经退出。

`StillRunning` 是非终态，Host 工具返回 `Info`，终端事件保持 `Running`。Host 命令结果同时携带 `ToolSuggestedAction.WaitForCompletion`，Agent Loop 的修复提示必须引导模型优先调用 `HostService.WaitCommandAsync` 续接同一终端会话；如果状态要求输入，再调用 `HostService.SendCommandInputAsync` 发送 stdin，不允许把等待预算耗尽误判为失败后重复启动相同命令。真正的最大运行时终止由运行时清理策略负责，避免 Agent Loop 因单次等待预算耗尽而进入错误的失败修复路径。

### 4.5.2 等待输入恢复

CLI 等待输入是当前终端会话的交互态，不等同于审批、凭证或业务补参这类产品化挂起交互。模型收到 `PromptUserInput` 且记录标记为 `终端输入` 或包含 `HostService.SendCommandInputAsync` 时，必须向同一会话发送 stdin；需要确认上下文时可先调用 `HostService.WaitCommandAsync` 获取最新输出。该路径不得重新执行原命令，也不得因为 `PromptUserInput` 这个动作名直接停止 Agent Loop。

### 4.5.3 模型可见终端预览

终端预览只服务 Agent Loop 与模型可见上下文，不替代完整日志事实源。预览规则如下：

1. 短输出保持原样，避免给模型增加无意义元数据。
2. 超长输出附带总字符数、总行数等摘要，并按头尾窗口保留最有诊断价值的命令回显、初期错误和最终报错。
3. 单行输出有独立字符预算，避免 minified 日志、长路径列表或二进制噪音占满预览。
4. 多行输出有独立行窗口，避免中间流水日志挤掉尾部失败信息。
5. 完整输出继续通过 `TerminalOutputBuffer` / `TerminalStream` 归档和读取，模型预览不得作为日志事实源。
6. 进程内缓冲触发内存水位裁剪后，模型可见的等待续接仍应保留当前窗口和裁剪标记；不得因为旧偏移越界返回空输出。
7. CLI 日志轮询读取 live 会话时，`GetRawOutput(sessionKey, startIndex)` / `GetStrippedOutput(sessionKey, startIndex)` 的返回值已是增量；`CliRuntimeCoordinator` 只能对 archived 完整日志做二次切片，避免前端非零偏移轮询拿到空 chunk。

终端归档预览与模型预览是两个不同职责：

1. `TerminalOutputPreviewBuilder` 面向模型上下文，强调头尾诊断信息和行/字符预算。
2. `TerminalArchivedOutputPreview` 面向数据库预览字段，强调保留最近输出，避免列表和摘要字段无限增长。
3. 两者都不能替代 `TerminalStream.ArchivedOutputPath` 指向的完整日志事实源。

### 4.5.4 终端输出文本清洗

终端输出进入模型上下文、实时事件和会话历史前必须先经过 `TerminalOutputTextSanitizer`：

1. 移除 ANSI 控制序列，避免颜色和光标控制污染模型上下文。
2. 移除 `\r`、`\n`、`\t` 之外的非文本控制字符，避免坏输出破坏 JSON 载荷、历史回放或 UI 渲染。
3. 等待输入识别基于清洗后的文本执行，保证彩色 password / confirm 提示仍能进入 stdin 续接流程。
4. `ProcessCliRuntimeHost` 的 raw buffer、stripped buffer 和实时输出事件共享同一清洗结果；完整归档仍通过终端输出事实源读取。

## 5. 当前收益

当前实现已经直接改善以下问题：

1. 终端状态读取不再散落在 Hub、RuntimeInspector 和 HostService 多处重复拼装。
2. 审批、日志、终止、回滚、等待终态已经有统一会话视图和稳定 DTO。
3. 高风险命令具备显式 checkpoint / rollback 主链。
4. Agent Loop 的恢复判断不再只是隐式代码路径，而是可替换的 recovery middleware。
5. 上下文溢出和大输出失败具备确定性恢复提示，不再盲目重复同一超长请求。
6. sandbox 生命周期和运行态协调职责已经基本从单一宿主类中拆开。
7. `ProcessCliRuntimeHost` 的文本清理、等待输入识别、命令完成协议、持久化映射和内部状态类型已拆到小组件，`TerminalOutputBuffer` 的观察摘要识别也已拆到 Core 小组件；后续新增能力必须继续沿职责边界拆分，避免运行时宿主和缓冲服务继续膨胀。
8. CLI 会话状态说明、用户下一步动作和等待输入提示由 Core 输出，客户端不再维护另一套主状态裁决。
9. CLI stdin 输入不再由 Hub 或宿主临时拼装，避免重复换行、重复状态事件和多入口预览不一致。
10. CLI 执行策略结果有稳定裁决码，后续审批 UI、Agent Loop 恢复和日志排查可以共享同一策略事实。
11. CLI 用户终止结果由 Core 构建并写回持久化事实源，协调器只负责标记运行态、终止进程、清理缓冲、保存结果和推送事件，避免后续轮询从旧状态回退。
12. 长时间运行的 CLI 命令不会因为单次工具等待预算耗尽被强制杀掉；模型收到信息态提示，用户和 Agent Loop 可以继续通过终端会话查看输出、等待终态或显式停止当前会话。

## 6. 当前结论

当前 CLI Runtime 主链具备以下结构：

1. `exec / process` 已有显式公共契约
2. `Queued / RolledBack` 已有统一状态值
3. warm pool 已升级为可消费的热 shell
4. CLI 日志读取已接入归档事实源
5. `CliExecutionRuntimeMapper` 已退化为 API 适配器，DTO 主映射收口到 Core
6. CLI 运行态摘要已成为 DTO 契约的一部分，终端主视图、聊天输入区和模型可见恢复路径可以共享同一低噪语义
7. CLI stdin 输入协议已进入 Core，聊天输入转发与显式 `SendCliInput` 共享同一规范化入口
8. CLI 执行策略裁决码已进入 Core，审批和阻断原因不再依赖散落的中文字符串
9. CLI 审批解决动作已进入 Core，单次授权、同类命令授权、拒绝和补充信息拥有统一结构化结果。
10. CLI 用户终止结果已进入 Core，缺失、已结束和成功终止三类结果共享同一 DTO 构建规则；成功终止会同步写回 `CliExecSession`，保证回读事实与事件推送一致。模型可见的 `StopCommandAsync` 也复用同一协调器终止主链，不直接操作进程注册表。
11. CLI 回滚结果已进入 Core，运行中阻断和成功回滚都返回统一状态快照；成功回滚会同步写回 `CliExecSession`，保证按钮反馈、事件推送和后续轮询看到同一事实。
12. CLI 命令等待结果已进入 Core，`StillRunning` 与失败/取消/进程不可用分离，HostService 不再依赖魔法退出码推断终端状态。
13. CLI 终端输出清洗已进入 Core，实时事件、模型可见输出和等待输入识别共享同一规则，降低异常控制字符污染会话历史的风险。
14. CLI 归档输出观察摘要已进入 Core，错误、警告、审批拦截和等待输入标签的识别与合并不再散落在缓冲持久化服务内。
15. CLI 数据库预览裁剪已进入 Core，`TerminalOutputBuffer` 不再维护预览裁剪常量和 banner 剥离逻辑。

CLI Runtime 的基础架构边界由上述契约、协调器、Sandbox 生命周期和归档日志事实源共同定义。
