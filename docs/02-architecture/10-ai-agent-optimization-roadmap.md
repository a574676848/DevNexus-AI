# AI Agent 优化路线图

本文档定义 DevNexus AI 在 Token 缓存、工具调用成功率、上下文压缩和可观测性方面的架构演进路线。目标是吸收主流 AI Agent 产品的成熟工程实践，并以可验证、可回滚、可上线的方式落地到现有系统。

## 设计目标

DevNexus AI 已具备 Swarm 编排、Skill/Plugin 体系、工具执行采集、Token 审计、RAG 上下文构建等基础能力。后续优化优先级如下：

1. **Token 缓存前缀稳定**  
   固定 system prompt、工具 Schema、AGENTS/Skill 指令注入顺序，将会话级动态内容后移到独立上下文块。

2. **工具调用成功率提升**  
   收敛核心工具面，强化 Schema 约束、参数预验证、结构化工具结果和失败修复策略。

3. **上下文压缩不污染用户对话**  
   会话摘要与后台压缩必须作为内部系统上下文管理，不以伪造用户消息的方式进入对话历史。

4. **指标闭环驱动优化**  
   成本、缓存命中率、工具成功率等收益必须来自审计数据，不以经验估算替代真实基线。

## 可借鉴实践

### OpenAI Codex / OpenAI API

可借鉴点：

- Agent Loop：推理、工具调用、结果回传、再次推理的循环。
- Prompt Caching：重复 prompt 前缀可被 Provider 复用，并通过缓存命中 Token 字段审计。
- Structured Outputs / Function Calling：工具参数使用严格 Schema 约束，降低参数缺失、枚举幻觉和格式错误。
- AGENTS.md 级联：项目级指令可提升工程任务一致性，但必须保证加载顺序稳定。

边界约束：

- 不照搬 CLI、IDE、Web、Desktop 等产品形态。
- 不把模型升级视为工具调用成功率的唯一解。

### OpenClacky

可借鉴点：

- 固定前缀：system prompt 和工具定义尽量不变，动态内容后移。
- 最小核心工具集：核心工具少而稳定，复杂能力通过 Skill 路由。
- 缓存边界意识：缓存标记和稳定消息顺序会影响命中率。
- 空闲压缩：用户空闲时做摘要和缓存预热。

边界约束：

- 不追求固定工具数量，核心工具集应由使用频率、失败率和 Token 成本决定。
- Skill 自进化必须等评估集、版本回滚和人工审核机制稳定后再推进。

### Anthropic Claude / Prompt Caching

可借鉴点：

- 缓存内容分层：长文档、工具说明、项目规范和稳定系统指令适合缓存。
- 动态内容后置：用户消息、检索结果和临时状态不应进入稳定前缀。
- 工具结果控量：工具结果会进入后续上下文，必须限制体积和格式。

### DeepSeek R1 / 推理型产品

可借鉴点：

- 过程可视化对用户有价值。

边界约束：

- 不展示或依赖模型完整 Chain of Thought。产品侧展示执行计划、工具状态、证据链和推理摘要。

## 当前基础

| 能力 | 当前落点 | 评价 |
|------|----------|------|
| ChatHistory 构建 | `ChatHistoryService`、`ChatSystemPromptBuilder`、`ChatHistoryMessageBuilder` | 已有 Token 预算与历史摘要基础，仍需强化前缀稳定性 |
| Kernel 会话隔离 | `KernelService.GetKernelForSessionAsync` | 已按 provider/session 缓存 Kernel，有利于插件上下文隔离 |
| 工具执行采集 | `ToolExecutionCollectorFilter` | 已记录工具成功、失败原因、建议动作和输出摘要 |
| Token 审计 | `TokenAuditFilter`、`ITokenAuditService` | 已有审计链路，并已扩展缓存与工具观测字段 |
| 工具确认 | `GlobalHostServiceInterceptor`、`ConfirmationService` | 已具备高风险操作确认基础 |
| 工具状态通知 | `ToolInvocationNotifier`、SignalR | 已支持前端展示工具运行状态 |
| Skill/Plugin | `SkillRegistry`、`PluginResolver`、`KernelService.Plugins` | 已有 Skill 与插件基础，仍需治理工具面 |

## Prompt 分层规范

| 层级 | 内容 | 是否进入稳定前缀 | 说明 |
|------|------|------------------|------|
| L0 | 全局系统角色、输出规范、安全边界 | 是 | 发布版本内尽量不变 |
| L1 | 核心工具使用规则、工具选择原则 | 是 | 只随工具契约版本变化 |
| L2 | 稳定工具 Schema 和插件描述 | 是 | 固定排序，生成 `ToolSchemaHash` |
| L3 | 项目级 AGENTS.md / 规范摘要 | 是，需稳定排序 | 内容变化时显式刷新缓存指纹 |
| L4 | 已固化 Session 摘要 | 部分稳定 | 摘要有版本号，避免每轮重写 |
| L5 | RAG 检索、Artifact 上下文、当前 Skill 命中 | 否 | 每轮动态后缀 |
| L6 | 最近对话和当前用户消息 | 否 | 放在最后 |

## 指标体系

### Token 缓存指标

| 指标 | 定义 | 数据来源 |
|------|------|----------|
| `InputTokens` | 本轮输入 Token 总数 | Provider usage / 本地估算 |
| `CachedPromptTokens` | Provider 返回的缓存命中 Token | Provider usage |
| `StablePrefixHash` | 稳定前缀内容指纹 | `ChatHistoryService` 构建阶段 |
| `ToolSchemaHash` | 工具 Schema 与排序指纹 | `KernelService.Plugins` 注册阶段 |
| `DynamicContextTokens` | RAG、Artifact、会话状态等动态内容 Token | `ChatPromptService` / `ChatHistoryService` |
| `HistoryTokens` | 历史消息 Token | `ChatHistoryMessageBuilder` |

### 工具调用指标

| 指标 | 定义 | 数据来源 |
|------|------|----------|
| `ToolName` | 工具名称，通常为 Plugin.Function | `ToolExecutionCollectorFilter` |
| `ToolArgumentsValid` | 参数是否通过预验证 | 参数验证器 / 执行分类 |
| `ToolFailureReason` | 工具失败原因 | `ToolExecutionResultClassifier` |
| `ToolSuggestedAction` | 失败后的建议动作 | `ToolExecutionResultClassifier` |
| `ToolRetryable` | 是否允许重试 | `ToolExecutionResultClassifier` |
| `ToolRequiresHumanIntervention` | 是否需要人工介入 | `ToolExecutionResultClassifier` |
| `ToolExitCode` | 工具退出码 | 工具执行结果 |

## 实施状态

| 任务 | 状态 | 产物 | 验证 |
|------|------|------|------|
| Phase 0.1 Token 缓存观测字段 | 已完成 | `ModelInvocationAudit`、`ModelInvocationAuditRecord`、Token 使用 DTO、`TokenAuditFilter`、审计查询映射、EF 迁移 `AddModelInvocationCacheAndToolMetrics` | `dotnet build .\src\DevNexus.sln --no-restore` 通过，0 警告 0 错误 |
| Phase 0.2 工具调用成功率审计扩展 | 已完成 | `ToolExecutionCollectorFilter`、工具审计字段、Token 使用 DTO、审计查询映射、EF 迁移 `AddModelInvocationCacheAndToolMetrics` | `dotnet build .\src\DevNexus.sln --no-restore` 通过，0 警告 0 错误 |
| Phase 0.3 工具调用烟测集 | 已完成 | `docs/06-development/tool-call-smoke-suite.md` | 烟测集覆盖 20 个工具选择、参数验证、失败分类与高风险审批场景 |
| Phase 0.4 缓存命中率与工具成功率看板 | 已完成 | `AiOptimizationDashboardDto`、`GetAiOptimizationDashboardAsync`、`GET /api/v1/AuditAnalytics/ai-optimization-dashboard` | 后端看板接口提供缓存命中率、工具成功率、失败原因和高频工具统计 |
| Phase 1 缓存前缀稳定化 | 已完成 | `PromptLayerMetadata`、`PromptFingerprint`、`PromptOptimizationMetadataDto`、`ChatSystemPromptBuilder` 分层构建、`ToolSchemaHash` 审计透传 | `dotnet build .\src\DevNexus.sln --no-restore` 通过，0 警告 0 错误 |
| Phase 2 工具调用可靠性 | 已完成 | `IToolCatalogService`、`InfrastructureToolCatalogService`、`IToolInvocationValidationService`、`DefaultToolInvocationValidationService`、工具参数预验证与结构化失败审计 | 参数格式错误可在执行前进入失败审计，工具输出按统一头尾策略压缩；工具协议名称、分类、风险等级和失败码已收口到共享常量 |
| Phase 3 动态工具集与 Skill 路由 | 已完成 | 核心工具默认注册、领域工具经 Skill 绑定注册、稳定排序工具目录、工具 Schema 指纹 | 普通会话默认不暴露 `HostService`、`CodeExecution`、`ImageGeneration` 等领域或高风险工具；具体工具目录实现位于 Infrastructure，Core 仅保留抽象接口 |
| Phase 4 上下文摘要与空闲压缩 | 已完成 | `ISessionSummaryService`、`SessionSummaryService`、会话摘要版本缓存、历史摘要 system message 注入 | 长历史压缩不再伪造成用户消息，摘要按内容 hash 复用 |
| Phase 5 安全与审批体验 | 已完成 | `AgentApprovalMode`、`AgentAutomationOptions`、统一审批超时常量、审批请求与结果审计、AI 优化看板前端视角 | 自动化模式可配置，高风险工具不绕过现有确认链路，审计看板可查看缓存与工具可靠性指标 |

## 路线图

### Phase 0：基线与仪表盘

目标：先量化，再优化。

任务：

1. [x] 扩展 Token 审计字段，记录 `CachedPromptTokens`、`StablePrefixHash`、`ToolSchemaHash`、`DynamicContextTokens`、`HistoryTokens`。
2. [x] 扩展工具审计字段，统计工具选择、参数验证、执行结果、失败类型、是否重试。
3. [x] 建立 20 条工具调用烟测集，覆盖核心工具链路。
4. [x] 增加缓存命中率与工具调用成功率看板。

验收标准：

- 能回答当前缓存命中率、缓存失效层级和高失败率工具。
- 任意优化前后可用同一指标对比。

### Phase 1：缓存前缀稳定化

目标：减少重复输入成本。

任务：

1. [x] 重构 prompt 分层：稳定前缀与动态后缀分离。
2. [x] 固定工具注册顺序与工具 Schema hash。
3. [x] AGENTS.md/Skill 指令按固定顺序加载，并生成规范摘要 hash。
4. [x] RAG、Artifact、当前 Skill 命中信息后移，不进入稳定 system prompt 主体。

验收标准：

- 同一会话连续请求中，动态上下文变化不影响稳定前缀 hash。
- Provider 返回缓存命中字段时，系统能看到缓存命中趋势。
- 日志能定位缓存失效原因。

### Phase 2：工具调用可靠性

目标：提升选对工具、传对参数、失败可修复的能力。

任务：

1. [x] 建立核心工具清单与领域工具清单。
2. [x] 为高频工具补齐参数 Schema、风险等级、结果契约。
3. [x] 增加工具参数预验证层。
4. [x] 将工具输出统一为短结构化结果，限制大输出进入模型上下文。
5. [x] 建立失败分类到动作的映射表。

验收标准：

- 参数格式错误被执行前拦截。
- 工具失败结果稳定归类。
- 核心工具评估集通过率达到可发布阈值，阈值建议先设为 85%，再逐步提高。

### Phase 3：动态工具集与 Skill 路由

目标：减少工具噪声，提高工具选择准确率。

任务：

1. [x] 改造工具启用计划，使用稳定排序工具目录和核心/领域工具分层。
2. [x] 默认只暴露核心工具，按任务启用领域工具。
3. [x] Skill 命中后只注册相关 Skill 绑定 Plugin，不把高风险工具默认暴露给普通问答。
4. [x] 为错误工具率、无必要工具率建立评估指标。

验收标准：

- 普通问答不会暴露高风险工具。
- 编码、联网、记忆、集成类任务能稳定启用正确工具。
- 工具 Schema 体积下降，工具调用成功率不下降。

### Phase 4：上下文摘要与空闲压缩

目标：控制长会话成本，避免上下文遗忘。

任务：

1. [x] 新增 `SessionSummaryService`，管理会话摘要版本。
2. [x] 长历史压缩为结构化摘要，不以用户消息形式注入。
3. [x] 空闲会话后台摘要，下一轮直接复用摘要块。
4. [x] 工具大输出只保留摘要、关键错误和尾部片段。

验收标准：

- 长会话 Token 增长曲线变缓。
- 摘要不会覆盖关键任务状态。
- 摘要变更可审计、可回滚。

### Phase 5：安全与审批体验

目标：提升自动化能力，同时保持可控。

任务：

1. [x] 引入审批模式：`Suggest`、`AutoEdit`、`FullAuto`。
2. [x] 高风险工具按模式控制执行。
3. [x] 将审批请求、用户决定、工具结果纳入审计。
4. [x] 为外部系统写操作保留幂等键和回滚建议扩展点。

验收标准：

- 自动化模式可配置、可审计。
- 高风险操作不会绕过确认。
- 工具失败后的用户提示清晰，不泄露无关内部细节。

## 暂缓事项

以下事项不进入第一阶段：

1. 完整 Skill 自进化。
2. 追求固定工具数量。
3. 展示完整 Chain of Thought。
4. 大规模重构 Swarm 编排。
5. 承诺固定成本下降比例。

## 架构与硬编码治理状态

本路线图对应的生产化收口遵循以下边界：

1. `IToolCatalogService` 保留在 Core 抽象层，具体工具目录实现落在 Infrastructure，避免 Core 层直接承载宿主、代码执行、图像生成等插件细节。
2. 工具协议名称、工具分类、风险等级、工具参数预验证键和 AI 优化看板路由统一收口到 `AiOptimizationConstants`，减少散落字符串。
3. 工具格式错误统一使用 `ToolFailureReason.ToolFormatError.ToWireValue()` 生成传输值，避免失败原因字符串漂移。
4. 审计看板前端视角键收口在组件私有常量中，页面文案保持中文友好，协议值与 UI 文案分离。

## 参考资料

- OpenAI Prompt Caching: https://platform.openai.com/docs/guides/prompt-caching
- OpenAI API Prompt Caching 发布说明: https://openai.com/index/api-prompt-caching/
- OpenAI Function Calling: https://platform.openai.com/docs/guides/function-calling
- OpenAI Structured Outputs: https://platform.openai.com/docs/guides/structured-outputs
- OpenAI Codex Agent Loop: https://openai.com/index/unrolling-the-codex-agent-loop/
- OpenAI Codex 开源仓库: https://github.com/openai/codex
- Anthropic Prompt Caching: https://platform.claude.com/docs/en/docs/build-with-claude/prompt-caching
- OpenClacky Features: https://www.openclacky.com/features
- OpenClacky Tech Deep Dive: https://www.openclacky.com/docs/tech-deep-dive
- OpenClacky Benchmark: https://www.openclacky.com/benchmark
