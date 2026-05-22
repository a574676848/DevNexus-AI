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
9. 挂起交互摘要 DTO（如 `PendingInteractionSummaryDto`）

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
5. `PendingInteractionSummaryBuilder`
6. `PendingInteractionResumeContextBuilder`
7. `CredentialRuntimeStatusResolver`
8. `ChatSessionRuntimeService`
9. `SwarmSessionControlService`
10. `AgentLoopToolValidationPolicy`

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
4. `HostTextPlugin` 与 `SwarmHostPlugin` 暴露 `WaitCommandAsync`、`SendCommandInputAsync` 与 `StopCommandAsync`，模型在收到 `WaitForCompletion` 后必须续接同一 CLI 会话，而不是重新启动相同命令；命令卡住或不再需要时可停止当前会话。
5. `HostService` 与 `CodeExecution` 这类受控执行工具必须返回 `[SUCCESS]`、`[FAILURE]`、`[INFO]`、`[EXCEPTION]` 或 `[SECURITY_BLOCKED]` 标签；检索与知识类工具可保留自然文本输出。
6. 工具目录中的 `SupportsParallelExecution` 是工具并行安全元数据，只读检索类工具可标记为支持并行；有状态或会修改宿主环境的工具默认不支持并行。
7. `SupportsParallelExecution` 必须参与 `ToolSchemaFingerprint`，避免并行安全边界变化时复用旧 Prompt 缓存。
8. `ToolInvocationConcurrencyPolicy` 是运行时并行调用事实源：只有当前 Kernel 已注册插件全部在目录中且均支持并行时，才允许模型一次请求多个工具并允许宿主并发执行；只要 `HostService` / `CodeExecution` / 未知插件参与，就必须串行执行，避免 CLI 会话、文件操作和 Swarm 工作包出现竞态。
9. `DefaultToolInvocationValidationService` 必须在工具执行前拦截明显无效的空对象参数：`HostService`、`CodeExecution`、`WebSearchPlugin` 和 `KnowledgeBasePlugin` 收到 `{}` 时归类为 `EmptyArguments`，避免模型把必填参数截断或遗漏后仍触发真实工具执行。
10. 工具目录只做低风险名称纠偏：大小写、空格、连字符和下划线差异可以解析；Provider 将工具名压成 `插件名/显式别名 + 安全分隔符 + 函数名` 时可还原规范插件名；没有分隔符的拼接名称和歧义名称必须返回空结果，不允许猜测。

## 5. Decision Runtime

### 5.1 Agent Loop 裁决

当前 Agent Loop 决策链为：

1. 工具输出经 `ToolExecutionCollectorFilter` 采集
2. `ToolExecutionResultClassifier` 生成结构化失败语义
3. `AgentLoopCompletionPolicy` 只在没有工具调用记录时允许普通完成收尾
4. `AgentLoopToolValidationPolicy` 在进入评估器前兜底验证工具调用协议
5. `LlmResponseEvaluator` / `RepairContextBuilder` 提供评估上下文
6. `ChatAgentLoopCoordinator` 根据结构化结果决定继续、停止或创建挂起交互

### 5.2 当前约束

1. 一旦工具结果标记 `RequiresHumanIntervention`，当前自动修复链必须停止。
2. 需要补参/审批时，必须转为 `PendingInteraction`，不得继续自动重试。
3. 内部修复提示不会进入用户可见消息主流，也不会作为历史 replay 消息进入后续 Prompt 缓存标记候选；当轮修复提示只作为当前重试输入追加。
4. 只要本轮存在工具调用记录，就不能仅依据模型文本或 Provider 完成原因判定任务完成，必须进入工具后处理与恢复前置判断。
5. `AgentLoopRecoveryGuard` 必须先读取统一运行态并让已有挂起交互优先停止自动修复；只有没有运行态阻塞时，才继续检查工具调用序列。
6. `AgentLoopExecutor` 入口必须再次验证工具调用序列，防止未来新增入口绕过 `AgentLoopRecoveryGuard`。
7. 缺少 `ToolCallId`、重复 `ToolCallId`、非法 JSON 参数、非 JSON 对象参数等协议错误必须停止自动修复。
8. 疑似截断的工具参数必须直接生成确定性小步重试提示，不能交给通用 LLM 评估器猜测。
   - 缺少闭合括号、混入 `<parameter>` / `</parameter>` 标签等 Provider 混流特征归类为疑似截断。
   - 完全不可识别的非法 JSON 仍按协议错误阻断自动修复，避免静默改写工具参数。
9. `AgentLoopExecutor` 必须先识别 `WaitForCompletion`、`StopCommand` 和 CLI stdin 续接这类确定性运行态恢复动作，直接构造运行态续接提示；不得先进入通用规则评估或 LLM 评估。
10. `AgentRuntimeRecoveryPromptBuilder` 只负责运行态续接提示，不得复用通用质量修复模板，也不得输出“质量评估失败”或伪造分数。
11. `AgentRuntimeRecoveryPromptBuilder` 必须合并重复运行态失败工具，并用 `occurrences` 标记出现次数；底层工具记录、事件流和日志事实仍保留完整明细。
12. `AgentRuntimeRecoveryPromptBuilder` 展示最近失败工具、策略说明和上一轮输出摘要时必须复用 `ToolOutputBudgetCompressor` 压缩文本，避免长 stderr 或长输出从任一段落撑大运行态续接提示。
13. `WaitForCompletion` 运行态续接提示必须显式引导调用 `HostService.WaitCommandAsync`，等待输入时再调用 `HostService.SendCommandInputAsync`，不得生成重新执行相同命令的通用重试提示。
14. `StopCommand` 运行态续接提示必须显式引导调用 `HostService.StopCommandAsync` 停止同一终端会话，不得把停止未完成误判为普通降级或重新启动命令。
15. `WaitForCompletion`、`StopCommand` 和 CLI stdin 运行态续接提示必须显式禁止调用 `HostService.ExecuteCommandAsync` 重新启动相同命令。
16. `LoopGuardMiddleware` 必须对连续 `StopCommand` 未闭环设置小型 watchdog；达到停止续接预算后停止自动修复，避免在同一终端停止动作上空转到最大重试次数。
17. `ToolExecutionResultClassifier` 位于 Core 层，是工具失败原因和建议动作的事实源；`[INFO]` 类型的 CLI 等待输入输出必须归类为 `PromptUserInput`，不能按普通成功处理。
18. CLI 等待 stdin 是同一终端会话的续接动作，不是产品化挂起交互；运行态续接提示必须使用“终端 stdin 续接”语义，引导模型调用 `HostService.SendCommandInputAsync`，必要时先调用 `HostService.WaitCommandAsync` 查看最新输出，不得使用产品化补参语义。
19. 受控执行工具缺少统一结果标签时，必须归类为 `ToolFormatError`，不能按自然文本成功处理。
20. CLI 续接工具响应必须包含 `recommendedTool`，让 Agent Loop 能在继续等待、发送 stdin、停止命令和总结结果之间选择确定的下一步。
21. `ToolExecutionResultClassifier` 必须消费 `recommendedTool`：`HostService.WaitCommandAsync` 归类为 `WaitForCompletion`，`HostService.SendCommandInputAsync` 归类为同会话 stdin 续接，`HostService.StopCommandAsync` 归类为 `StopCommand`。
22. CLI stdin 续接必须先确认当前会话存在且仍活跃；未知会话直接返回 `[FAILURE]`，终态会话返回终态结果供模型总结，不得创建兜底运行态会话。
23. CLI 停止命令找不到会话时必须返回 `[FAILURE]` 且推荐 `ReviewResult`，不能把未知会话误判为已成功停止活跃命令。
24. LLM 读取超时或响应超时应进入确定性小步恢复提示：压缩上下文、拆分目标、分批读取和缩小输出；普通连接失败不触发该提示，仍走原有重试或备用路径。
25. 历史压缩后的最近消息片段必须通过 `ChatHistoryRecentSlicePolicy` 保证具备用户锚点；摘要之后不得直接拼接孤立助手消息，避免 Provider 将无用户锚点的 assistant 片段误判为非法或已完成上下文。
26. `ChatHistoryMessageBuilder` 只回放已完成的助手消息；生成中、取消、错误和截断的助手消息属于不完整 turn，不得进入后续模型历史、历史压缩输入或 Prompt 缓存标记候选。
27. `ChatHistoryMessageBuilder` 必须通过 `ChatHistoryReplayTextSanitizer` 清理进入模型历史回放的文本，移除 ANSI 控制序列和非文本控制字符；清洗后的文本同时用于直接回放、历史压缩输入和最近片段，避免终端或工具输出污染后续 Prompt、缓存标记与摘要。
28. `ChatHistoryMessageBuilder` 必须输出 `ChatHistoryGovernanceSnapshot`，记录历史预算、实际写入 Token、压缩策略、摘要覆盖消息数、最近片段消息数、压缩索引以及被跳过的内部修复和未完成助手消息数；压缩索引只保留覆盖消息数、摘要长度、摘要稳定指纹和少量主题提示，不保存完整摘要正文；后续任务编排、记忆沉淀和自我迭代只能消费该结构化快照或日志，不应从 Prompt 文本反推上下文边界。
29. 记忆沉淀触发必须通过 `MemoryConsolidationTriggerPolicy` 归纳决策，并通过 `ChatHistoryPressurePolicy` 解释上下文压力：消息增量达到阈值、历史新进入摘要压缩窗口、历史被预算截断或存在未完成助手 turn 时可立即入队；普通会话增量只调度空闲延迟任务；消息不足或无新增消息时不创建后台任务；摘要压缩压力必须以 `CompressedMessageCount` 和 `LastConsolidatedMessageCount` 形成窗口基线，已沉淀覆盖的压缩历史不得每轮重复立即入队；触发决策必须保留 `ContextPressureReason`，供任务编排和自我迭代复用。
30. 单轮任务编排必须通过 `AgentTaskOrchestrationSnapshotBuilder` 汇总上下文治理、工具事件、Agent Loop 决策和记忆沉淀决策，输出 `[AI.Task.Orchestration]` 结构化日志；该快照必须记录上下文压力布尔值、`ContextPressureReason`、历史压缩索引和压缩摘要指纹，用于后续自我迭代和后台分析，不进入产品化审计看板。
31. 经验提纯触发必须先经过 `SelfIterationCandidatePolicy` 判断。Agent Loop 重试中、已停止或仍有工具恢复动作时只观察不提纯；上下文压力已处理、立即记忆沉淀、工具工作流完成或长回复完成时才调度经验提纯，避免每轮都写长期经验；上下文压力触发时必须消费 `ContextPressureReason`，区分摘要压缩、预算截断和未完成助手消息跳过；候选决策必须带出 `ContextPressureReason` 和压缩摘要指纹，通过 `SelfIterationCandidateMetadata` 以低噪字段写入完成态 AI 消息 metadata，并通过 Domain 层 `ExperienceDistillationScheduleContext` 随经验提纯调度进入后台任务；复用经验只在消息 metadata 中保留 `CitationFingerprint`，详细来源继续由系统经验 `ContextTags` 和 `SystemExperienceMemoryCitation` 解析，避免重复持久化；新生成的系统经验必须把候选原因、上下文压力原因和压缩摘要指纹写入 `ContextTags`，不新增数据库字段，不回填摘要正文；`[AI.SelfIteration]` 与 `[AI.SelfIteration.Review]` 日志只记录结构化事实，不进入产品化审计看板，且不得作为唯一闭环承载。
32. 系统经验提纯 Prompt 和输出解析归属 Core 层：`ExperienceDistillationOutputProtocol` 定义版本、`NONE`、`[INTENT]` 标记、提纯协议标签、高价值经验信号、高价值信号关键词、拒绝条件、跳过条件关键词、原始记录禁入标记、SOP 持久化长度上限和 `distillation-prompt-fingerprint` 标签前缀，`ExperienceDistillationPromptBuilder` 使用该协议构建稳定 `ExperienceDistillationPrompt` 值对象并提供 `Fingerprint`，默认拒绝一次性解释、普通问答、重复经验、原始 QA、日志和工具输出，只有命中长期价值信号才输出 SOP；`ExperienceDistillationParser` 负责解析协议输出和 SOP 正文，必须拒绝缺少显式 `[INTENT]` 标记、`NONE` 后混入正文、包含 Markdown 代码块、SOP 超长或混入原始记录标记的输出；`ExperienceDistillationJob` 只负责读取消息、调用模型和保存经验，复盘日志只记录 `DistillationPromptFingerprint`，不记录完整 Prompt，也不再持有硬编码 Prompt 或解析规则。
33. 系统经验提纯准入、问答对选择和实体默认值归属 Core 层：`ExperienceDistillationQaPairSelector` 选择最近相邻的完成态用户-助手文本对，`ExperienceDistillationAdmissionPolicy` 判断 QA 是否足够，并复用 `ExperienceDistillationOutputProtocol.SkipConditionKeywords` 在 LLM 前拦截一次性测试、格式化、提交、部署或临时排查，再复用 `HighValueSignalKeywords` 判断是否包含决策、偏好、约束、流程、踩坑或修复等长期价值信号后才进入 LLM 提纯；`ExperienceDistillationAdmissionDecision` 必须保留命中的跳过关键词或价值关键词，后台日志只能记录关键词事实，不展开原始 QA；`ExperienceDistillationExperienceFactory` 统一设置初始效用评分、使用次数、匹配时间，并把提纯协议版本、Prompt 指纹、命中的价值关键词和来源会话 ID 写入 `ContextTags`；Infrastructure 不直接散落配对规则、长度阈值、经验价值信号、生命周期默认值或协议来源标签。
34. 系统经验保存必须先通过 `SystemExperienceFingerprint` 和 `SystemExperienceDuplicatePolicy` 做重复写入判定。指纹写入现有 `ContextTags`，不新增迁移；保存服务先按同类型读取候选，再由 Core 策略跳过相同语义指纹或已有指纹标签的重复经验，不能只按原始 `Intent` 精确匹配候选。
35. 系统经验检索、命中增强、重复再发现反馈和修剪淘汰必须通过 `SystemExperienceLifecyclePolicy` 归纳阈值和评分规则。Infrastructure 只负责数据库与向量库读写，不直接散落相似度、衰减天数、衰减倍率或淘汰分数；重复经验跳过新增时必须强化已有经验的使用次数、效用评分和最近命中时间。
36. 系统经验保存结果必须通过 `SystemExperienceSaveResultFactory` 返回结构化事实，区分新增、重复跳过和向量索引失败，并在 `ExperienceSaveResultDto` 中携带 `MemoryCitation`、`AttemptMemoryCitation`、`CitationFingerprint` 与 `AttemptCitationFingerprint`；命中的持久经验和本次保存尝试必须各自保留引用事实，新建或索引失败时两者一致，重复跳过时两者可不同；后台提纯任务记录该结果，供后续自我迭代判断真实沉淀收益和重复再发现来源，不能只按任务执行成功推断经验已新增。
37. 自我迭代复盘必须通过 `SelfIterationReviewPolicy` 消费经验保存结果或跳过原因，跳过细分原因由 `SelfIterationSkipReasons` 统一定义，跳过原因到复盘原因的归类也必须在 Core 层完成；不同阶段不得复用同一个原因值，例如前置 QA 缺失和准入 QA 缺失必须保留可区分标记。新增且索引成功视为形成长期经验，重复经验只记录观察，新增但索引失败标记为需要修复关注；消息不足、Swarm 会话、无 QA 对、Provider 缺失、模型超时、准入拒绝和解析拒绝也必须输出观察型 `[AI.SelfIteration.Review]`，记录 `SkipReason` 但不创建长期经验；准入拒绝必须记录命中的跳过关键词或价值关键词，准入后解析拒绝和保存完成也必须保留命中的价值关键词，解析拒绝包含 SOP 超长和原始记录泄漏这类持久化质量拒绝，并通过 `SystemExperienceMemoryCitation.CreateUnpersistedDistillationCitation` 基于来源会话、价值信号、提纯协议和提纯 Prompt 指纹生成未落盘引用事实，再由 `SelfIterationReviewDecision.MemoryCitation` 透传 `CitationFingerprint`；重复保存完成还必须同时记录本次尝试的 `AttemptCitationFingerprint`；复盘决策必须透传保存结果中的两份 `SystemExperienceMemoryCitation`，不从日志字符串反推来源事实；该结果不进入产品化审计看板。
38. 系统经验回放必须通过 `SystemExperienceReplayPolicy` 决策。完全命中可直接返回经验答案；部分命中只能通过 `SystemExperienceReplayContextBuilder` 生成动态上下文并写入 `ChatMessageMetadataKeys.SystemExperienceContext`，再由 `ChatSystemPromptBuilder` 注入 `dynamic.system_experience` 片段；动态上下文必须通过 `SystemExperienceMemoryCitation` 渲染 `MemoryCitation`，仅暴露经验 ID、来源会话 ID、价值信号、提纯协议版本、提纯 Prompt 指纹和稳定引用指纹，不展开原始 QA；不得改写用户原始请求，也不得把系统经验伪装成用户消息。
39. 系统经验回放事实必须通过 `SystemExperienceReplaySnapshot` 进入 `PromptLayerMetadata` 和 `AgentTaskOrchestrationSnapshot`。任务编排日志需要记录是否已回放、是否注入经验、是否直接返回、经验 ID、相似度、回放原因、提纯协议标签、自我迭代调度事实以及 `SystemExperienceMemoryCitation` 中的价值信号关键词、来源会话 ID、提纯 Prompt 指纹和引用指纹；自我迭代遇到已复用系统经验的回合默认只观察，不重复提纯相同经验，并在观察型候选决策中保留复用经验的候选原因、上下文压力原因、压缩摘要指纹、提纯 Prompt 指纹和 `SystemExperienceMemoryCitation`，不得从原始标签或 Prompt 文本反推引用事实。
40. 系统经验完全命中直接返回也必须走完成闭环：AI 消息状态置为 `completed`，发送 `IsLast` 块，同步搜索索引，触发记忆沉淀检查，记录任务编排快照，并输出 `[AI.SelfIteration]` 观察型候选评估；不得因为跳过 LLM 流式生成而绕过完成后置动作。
41. 系统经验回放 metadata 必须通过 `SystemExperienceReplayMetadata` 统一读写。动态上下文回放和直接命中回放共享 `SystemExperienceId`、`SystemExperienceSimilarity`、`SystemExperienceReplayReason` 与 `SystemExperienceContextTags`，直接命中额外写入 `cacheHit` 和 `similarity`；`SystemExperienceContextTagSnapshot` 负责把标签解析为提纯协议、自我迭代候选原因、上下文压力原因、压缩指纹、提纯 Prompt 指纹、价值关键词、来源会话 ID 和语义指纹；`ChatSystemPromptBuilder` 只能通过该 helper 读取回放快照，不得在不同流程中手写或手动解析同一组字段。
42. `SystemExperienceReplayMetadata.BuildSnapshot` 必须按 `SystemExperienceReplayReason` 还原回放方式：`direct-answer` 映射 `AnsweredDirectly=true`，`dynamic-context` 映射 `InjectedDynamicContext=true`；不得只因为存在 replay metadata 就默认当作动态上下文注入。

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

### 6.2 摘要协议

挂起交互的用户可见摘要由 Core 层 `PendingInteractionSummaryBuilder` 统一生成，并通过 Shared 层 DTO 下发：

1. `PendingInteractionDto.Summary`：单个挂起交互的标签、说明、输入占位和下一步动作。
2. `ChatSessionRuntimeDto.PrimaryPendingInteractionSummary`：当前会话主阻塞交互的低噪摘要。
3. 客户端只消费摘要字段并保留旧载荷兜底，不重新按 `Kind` / `Title` / `Description` 推断展示文案。

### 6.3 当前行为

1. 同会话、同来源工具、同类交互会优先复用活跃项。
2. 创建后会立即推送结构化运行时事件，前端据此回拉最新挂起交互列表。
3. 解决后会推送最新挂起列表，并根据状态推送 `PendingInteractionResolved`。
4. 后台服务会周期性将过期项标记为 `Expired`，并推送 `PendingInteractionExpired`。
5. 队列拒绝提示、运行时恢复停止提示和前端输入框占位文案复用同一挂起交互摘要，避免不同入口对同一阻塞原因给出不同表述。
6. 审批或补充信息解决后会写入 `resolutionAction`，审批通过还会写入 `approvalScope`；恢复消息 metadata 也会携带同一组键，后端 Prompt 动态上下文可识别这是挂起交互恢复，而不是普通用户输入。
7. 恢复上下文由 Core 层 `PendingInteractionResumeContextBuilder` 生成，只输出语义化的解决动作、审批范围和用户补充字段；`resolutionAction` / `approvalScope` 这类内部 metadata 键不直接暴露给模型。

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
