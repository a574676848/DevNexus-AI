# 测试与校验

本文档只说明当前仓库可直接采用的校验方式。

## 测试覆盖

自动化测试位于 `src/tests/DevNexus.Core.Tests`，覆盖 Core 层纯逻辑，不依赖 Infrastructure、数据库或 UI。主要覆盖领域包括：

- 工具调用序列验证、截断恢复、参数预验证、Schema 指纹
- CLI 运行时会话管理、命令哨兵协议、stdin 输入、终端输出清洗与预览
- Agent Loop 恢复策略、运行时续接、完成判定、停止协议
- Prompt 缓存标记、片段清单、漂移分析、Token 指标
- Swarm 会话状态摘要、取消/失败收尾、工作包生命周期
- 事件批次诊断、工具执行耗时与失败分析
- 挂起交互状态摘要与恢复上下文

## 当前校验方式

### 1. 先恢复并构建

```bash
dotnet restore src/DevNexus.sln
dotnet build src/DevNexus.sln
```

### 2. 运行自动化测试

```bash
dotnet test src/DevNexus.sln --no-build
```

### 3. 再做主流程冒烟验证

至少验证以下流程：

1. 登录与聊天流式输出
2. 文件上传与解析状态回传
3. 文件任务创建与结果回灌
4. 设置页中的系统信息、更新检查和发布中心

### 4. 涉及数据库或迁移时

额外确认：

- 应用能正常启动
- 迁移能应用成功
- 相关页面和接口能返回预期结果
- Prompt 片段清单类审计字段只保存摘要字段，不保存原始 Prompt 文本
- 动态上下文清单只保存来源、字符数和摘要 hash；不得参与 `PromptCacheKey` 或产品化审计看板
- Prompt 缓存、稳定前缀、工具 Schema、缓存标记和片段漂移类指标只作为非产品化诊断进入日志系统，不进入审计看板、审计 DTO 或公开审计 API
- 平均输入与平均未命中输入指标按模型请求数归一，避免用总 Token 掩盖单轮成本变化
- 工具执行采集记录、事件摘要与失败摘要必须共享 `ToolOutputBudgetCompressor`，避免同一工具输出在模型上下文、UI 摘要和自动修复链路里出现不同截断语义
- 多工具失败摘要必须包含工具名，按工具名和摘要文本去重后再限制条数，避免自动修复无法定位错误来源
- Agent Loop 工具调用协议必须通过 `AgentLoopToolValidationPolicy` 和 `ToolExecutionSequenceValidator` 双层验证；截断类参数直接生成小步重试提示，非截断协议错误停止自动修复
- 工具参数非法 JSON 需要区分原因：缺少闭合括号或混入 `<parameter>` 标签归类为 `TruncatedArguments` 并进入确定性小步修复；完全不可识别的非法 JSON 仍归类为 `InvalidJson` 并停止自动修复
- 工具调用参数必须是 JSON 对象；合法 JSON 数组、字符串或数字同样应归类为 `NonObjectArguments`，由预验证和 Agent Loop 序列验证阻断，避免非对象 payload 进入真实工具执行。
- Agent Loop 通用修复提示中的重复同类工具失败必须合并展示，并保留 `occurrences`；底层工具执行记录和事件流仍保留完整事实
- Agent Loop 对 `ToolSuggestedAction.WaitForCompletion` 的修复提示必须包含 `HostService.WaitCommandAsync` 与 `HostService.SendCommandInputAsync`，并明确禁止重新启动相同命令
- Agent Loop 通用修复提示必须包含原始用户目标片段，并对过长目标做固定长度截断，避免自动修复偏离用户目标或提示自身膨胀
- Agent Loop 对 CLI 等待 stdin 的 `PromptUserInput` 修复提示必须引导调用 `HostService.SendCommandInputAsync`，不得把终端输入升级成产品化挂起交互
- Agent Loop 对 `WaitForCompletion`、`StopCommand` 和 CLI stdin 续接必须先走确定性恢复提示，不得先进入通用规则评估器或 LLM 评估器；非终端补参不能误判为 CLI stdin 续接
- Agent Loop 运行态续接提示必须由 `AgentRuntimeRecoveryPromptBuilder` 生成，不得复用通用质量修复模板；提示中不能出现“质量评估失败”或伪造分数
- Agent Loop 运行态续接提示必须显式禁止调用 `HostService.ExecuteCommandAsync` 重新启动相同命令，避免模型在 wait/stdin/stop 场景选错工具
- Agent Loop 运行态续接提示中的 CLI stdin 场景必须使用“终端 stdin 续接”语义，不得出现“请先补充必要输入”或“工具恢复需要补充输入”这类产品化补参文案
- Agent Loop 运行态续接提示中的重复失败工具必须合并展示并保留 `occurrences`；底层工具记录、事件流和日志事实仍保留完整明细
- Agent Loop 运行态续接提示中的策略说明、单条失败摘要和上一轮输出摘要必须复用 `ToolOutputBudgetCompressor` 压缩，避免长 stderr 或长输出从任一段落撑大恢复提示
- Agent Loop 恢复前置必须先读取统一运行态，已有挂起交互优先级高于工具序列异常；CLI stdin 续接类 `PromptUserInput` 不创建产品化挂起交互；二次尝试后的不可恢复失败应停止自动修复
- Agent Loop 连续 `StopCommand` 未闭环必须由 `LoopGuardMiddleware` 停止自动修复，避免同一终端停止动作空转到最大重试次数；首轮停止续接仍应继续
- Agent Loop 停止协议必须通过 `AgentLoopStopSignalPolicy` 判定；只有最后一个非空行独立等于 `[AGENT_LOOP_STOP]` 才停止，正文引用该标记不得误停
- `ToolExecutionResultClassifier` 必须覆盖 CLI `[INFO]` 运行中和等待输入输出；运行中或 `recommendedTool=HostService.WaitCommandAsync` 归类为 `WaitForCompletion`，等待输入或 `recommendedTool=HostService.SendCommandInputAsync` 归类为 `PromptUserInput`，停止未闭环且 `recommendedTool=HostService.StopCommandAsync` 归类为 `StopCommand`
- `ToolExecutionResultClassifier` 必须覆盖受控执行工具缺少统一标签的场景：开启标签强制模式时归类为 `ToolFormatError`，默认模式仍兼容检索类自然文本成功输出
- LLM `read timeout` 或响应超时必须进入小步恢复提示：压缩上下文、拆分目标、分批读取和缩小输出；普通连接失败不能触发该提示
- CLI 工具调用返回给模型的终端输出预览必须使用 `TerminalOutputPreviewBuilder` 保留总字符数、总行数、头部和尾部，并限制单行长度与行窗口；完整终端日志仍以归档输出为事实源
- CLI 终端输出进入实时事件、模型上下文和等待输入识别前必须使用 `TerminalOutputTextSanitizer` 清理 ANSI 与非文本控制字符，避免异常输出污染会话历史
- CLI 终端归档观察摘要必须使用 `TerminalOutputWatchSummaryBuilder` 识别并合并错误、警告、审批拦截和等待输入标签；`TerminalOutputBuffer` 不应维护重复正则规则
- CLI 终端数据库预览字段必须使用 `TerminalArchivedOutputPreview` 裁剪；完整日志仍以归档文件和日志读取接口为事实源，不得把预览字段当完整输出
- CLI 命令完成判断必须使用 Core 层 `CliCommandCompletionProtocol` 包装命令并解析哨兵、干净输出和退出码，避免运行时宿主散落平台分支和字符串协议
- CLI 命令完成协议必须先保存原始命令退出码，再输出哨兵和退出码；测试必须覆盖 POSIX `$?` 与 PowerShell `$LASTEXITCODE` 不被 `echo` 覆盖，并覆盖 PowerShell cmdlet 无 `$LASTEXITCODE` 时回退 `$?`
- CLI 命令等待结果必须使用 `CliCommandExecutionResult` 表达；等待预算耗尽但命令仍在运行时必须返回 `StillRunning`，工具文本格式化为 `[INFO]`，并携带 `ToolSuggestedAction.WaitForCompletion`，不得进入失败恢复链路或把终端事件标记为失败终态
- CLI 会话主状态、低噪说明和下一步动作必须使用 Core 层 `CliRuntimeStatusSummaryBuilder` 生成；客户端只消费 `CliSessionStateDto.StatusSummary` 并保留旧载荷兜底
- CLI stdin 输入必须通过 Core 层 `CliRuntimeInputProtocol` 规范化；末尾换行要在协议层收敛，空输入保留为空行语义，Api 层不得重复广播同一次输入产生的 CLI 状态事件
- CLI 模型可见续接必须通过 `HostTextPlugin.WaitCommandAsync` / `SendCommandInputAsync` 进入 `ICliRuntimeCoordinator`；缺少会话上下文必须返回 `[FAILURE]`，活跃会话返回 `[INFO]`，终态会话返回 `[SUCCESS]`
- CLI 模型可见等待预算必须由 Core 层 `CliContinuationWaitBudgetPolicy` 统一管理；`HostTextPlugin` 与 `SwarmHostPlugin` 不得各自硬编码 wait timeout 上下限
- CLI stdin 续接必须先确认当前会话存在且活跃；未知会话返回 `[FAILURE]`，终态会话返回终态快照，不得创建兜底运行态会话
- CLI 模型可见续接响应必须包含 `recommendedTool`：运行中推荐 `HostService.WaitCommandAsync`，等待输入推荐 `HostService.SendCommandInputAsync`，停止后仍活跃推荐 `HostService.StopCommandAsync`，终态推荐 `ReviewResult`
- CLI 模型可见停止命令找不到会话时必须返回 `[FAILURE]` 和 `recommendedTool=ReviewResult`，不能继续推荐 `StopCommandAsync`
- CLI 模型可见停止必须通过 `HostTextPlugin.StopCommandAsync` 进入 `ICliRuntimeCoordinator.TerminateAsync`；不得绕过协调器直接操作进程注册表
- Swarm 工作包模型可见续接和停止必须通过 `SwarmHostPlugin.WaitCommandAsync` / `SendCommandInputAsync` / `StopCommandAsync` 进入 `ICliRuntimeCoordinator`；不得绕过协调器直接操作进程注册表
- Swarm 工作包调度取消必须覆盖两类场景：取消已触发时不启动新工作包；执行中收到取消时当前包标记为 `Aborted`，不得落入普通失败分支
- CLI 用户终止结果必须通过 Core 层 `CliTerminationResultBuilder` 构建并写回 `CliExecSession`；Infrastructure 只负责标记运行态、终止进程、清理缓冲、保存结果和推送事件，不手写终止状态语义
- CLI 回滚结果必须通过 Core 层 `CliRollbackResultBuilder` 构建并写回 `CliExecSession`；SignalR 返回值和 `CliExecRolledBack` 事件必须携带统一 `State`
- CLI 执行策略分支必须验证 `CliExecutionPolicyResult.DecisionCode`；审批、阻断、重复命令循环等原因不得依赖解析中文 `Message`
- CLI 审批解决必须验证 `resolutionAction` 与 `approvalScope` 会写入 `PendingInteraction.ResolutionData` 和恢复消息 metadata；审批通过后即使没有表单值，也必须进入 Agent Loop 恢复上下文
- 挂起交互恢复上下文必须使用 `PendingInteractionResumeContextBuilder` 生成，内部 metadata 键不得重复暴露给模型，只输出语义化解决动作、审批范围和用户补充字段
- Swarm 主状态、阶段计数和低噪展示文案必须由 Core 层 `SwarmSessionStatusSummaryBuilder` 统一生成；REST、Hub 初始快照和实时事件复用同一摘要，客户端只做展示与旧载荷兜底
- Swarm 暂停、继续、中止和工作包重试控制事件必须通过 Core 层 `SwarmControlCommandBuilder` 生成 `SwarmControlCommandDto`，事件载荷必须携带 `StatusSummary`
- Swarm 暂停、继续和工作包重试命令必须通过 Core 层 `SwarmControlCommandPolicy` 判定是否可执行；终态会话继续、暂停或不可重试工作包必须返回 `Accepted=false`，客户端不得切换为成功态
- Swarm 会话中止或注销必须清理 `SwarmSessionRegistry` 中该会话的工作包重试占位，避免陈旧单飞标记导致后续同包重试被误拒
- Swarm 取消、异常和未完成工作包收尾必须由 Core 层 `SwarmSessionFinalizationPolicy` 生成；已完成工作包不得被降级，用户主动中止的未完成持久化工作包必须标记为 `Skipped`，异常恢复的未完成持久化工作包必须标记为 `Failed`，并补齐终态、原因和结果摘要
- Swarm 启动恢复必须复用 `SwarmSessionFinalizationPolicy` 补齐未完成工作包终态；不能只把会话标记为失败却留下 `InProgress` / `Ready` 工作包
- Swarm 工作包记录生命周期必须由 `SwarmPackageRecordLifecyclePolicy` 维护；终态工作包必须补齐 `CompletedAt`，重试重新进入执行态时必须清空旧 `CompletedAt`
- PendingInteraction 主状态、阻塞说明、输入占位和下一步动作必须由 Core 层 `PendingInteractionSummaryBuilder` 生成；队列拒绝、运行时恢复停止提示和前端输入框占位复用同一摘要，客户端只做展示与旧载荷兜底
- 工具执行批次总耗时来自 Core 事件诊断摘要，客户端不重新计算工具耗时；聊天主界面不直接展示该内部指标
- 工具执行最慢工具诊断由 Core 稳定选择，耗时相同时按工具名排序，用于审计和性能排查，不进入聊天主界面低噪摘要
- 工具执行首个失败工具与失败摘要由 Core 批次诊断提供，客户端只展示失败工具、失败原因和下一步建议，不从事件明细重新推断
- 工具执行批次建议动作展示文本由 Core 批次诊断提供，客户端不重新映射 `ToolSuggestedAction`
- 工具执行批次建议动作在原始工具记录可用时复用 Core 恢复策略推导，避免缺少显式 `SuggestedAction` 时丢失刷新凭证、重试或降级提示
- 工具建议动作恢复优先级和批次诊断短文本由 Shared 枚举扩展统一提供，避免 Core 与 UI 分别维护重复映射
- Agent Loop 完成判定必须使用 `AgentLoopCompletionPolicy`；只要存在工具调用记录，就必须进入工具后处理，不能仅依据模型文本或 Provider 完成原因收尾
- 工具名规范解析只容忍大小写、空格、连字符和下划线差异；真实插件名带 `Plugin` 后缀时必须通过工具目录显式别名解析；歧义名称必须返回空结果，不允许猜测并错误注册工具；Provider 将工具名压成 `插件名/显式别名 + 安全分隔符 + 函数名` 的单段调用名时可解析回规范插件名，但无分隔符拼接名称不得纠偏
- 工具调用名称结构必须先通过 `ToolInvocationNameParser` 解析；`functions.` / `tools.` 等 Provider 包装前缀不得被当作插件名
- 工具参数预验证必须先解析规范插件名，保证通过别名进入的 WebSearch / KnowledgeBase 调用仍执行空查询参数校验；HostService、CodeExecution、WebSearch 和 KnowledgeBase 收到 `{}` 空对象参数时必须归类为 `EmptyArguments`，不能继续触发真实工具执行
- 工具 Schema 指纹必须包含 `ToolSchemaFingerprint.ProtocolVersion`；工具名解析、Provider 包装前缀、别名协议、受控输出标签要求或参数预验证边界变化时，应刷新 `ToolSchemaHash` 和 `PromptCacheKey`
- 工具目录的 `SupportsParallelExecution` 必须纳入 `ToolSchemaFingerprint`；只读检索类工具可标记并行安全，有状态或会修改宿主环境的工具默认不支持并行
- 工具调用并发必须通过 `ToolInvocationConcurrencyPolicy` 判定；只有当前注册插件全部在工具目录中且均支持并行时，才允许 `AllowParallelCalls` 和 `AllowConcurrentInvocation`，HostService、CodeExecution 或未知插件参与时必须串行
- Prompt 缓存标记预热诊断由 Core 从最终 ChatHistory 中选择最近两个 `User` / `Assistant` 真实对话消息，system prompt、工具输出、动态摘要类系统消息和内部修复提示不计入候选；诊断结果和 `CacheMarkerReadinessReason` 写入 `[AI.Prompt.Diagnostics]` 结构化日志
- Prompt 动态上下文、文档上下文和 RAG 检索片段以系统注入型 user message 后移，不拼回稳定 system prompt，并且不作为缓存标记候选
- 历史压缩后的最近消息片段必须通过 `ChatHistoryRecentSlicePolicy` 保证具备用户锚点；当切片边界落在孤立助手消息上时，应从下一条用户消息开始 replay，不能把摘要后第一条真实历史构造成 assistant
- 聊天历史 replay 必须过滤未完成助手消息；`in_progress`、`cancelled`、`error`、`truncated` 状态的 assistant 消息不得进入模型历史、历史压缩输入或 Prompt 缓存标记候选
- 记忆沉淀触发必须覆盖摘要压缩窗口基线：当 `LastConsolidatedMessageCount` 已覆盖 `CompressedMessageCount` 时，摘要压缩只能调度空闲延迟任务，不能每轮重复立即入队；预算截断和未完成助手消息跳过仍保留即时保护
- Prompt 缓存漂移统计必须按 `SessionId + PromptCacheNamespace` 比较相邻请求；Provider 或模型切换不能被误判为稳定前缀或工具 Schema 漂移，且不能写入 `PromptCacheKey`
- Prompt 片段清单必须保留 `Source` 来源标识，审计落库仍只保存来源、顺序、字符数和内容指纹，不保存原始 Prompt 文本；来源变化不参与 `PromptCacheKey`

## 测试边界

正式测试项目以仓库实际目录为准。没有测试项目覆盖的改动，需要在提交说明中写明手动冒烟验证范围。
