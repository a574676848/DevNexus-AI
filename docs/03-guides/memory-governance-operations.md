# 记忆治理与回放效果验收指南

本文档面向维护者和现场排障人员，说明如何判断系统经验是否真的提升回答质量，并识别上下文污染、不可追踪复用和低价值经验沉淀风险。

## 运行时观测入口

聊天回合完成编排快照时，会旁路写入 `[AI.Memory.ReplayEvaluation]` 结构化日志。该日志同时覆盖直接命中返回与动态上下文注入两条真实回放路径，包含 `ReplayReason`、`UsefulRecall`、`ContextPollutionRisk`、`UntraceableReuseRisk`、`EvaluationReason`、`Similarity` 及引用事实完整性标志。

该日志只用于效果分析和治理复盘，不进入产品化审计表、公开 API、数据库字段或 `PromptCacheKey`，也不改变回放分支选择。

## 适用范围

适用于以下场景：

- 聊天命中了系统经验，但回答质量没有明显提升。
- 动态上下文注入后，模型偏离当前用户请求。
- 经验直接命中返回了答案，但无法追溯来源会话或提纯事实。
- 维护者需要判断记忆召回是否值得保留、强化或淘汰。
- 发布前需要验证记忆链路不会把一次性日志、临时排查或原始 QA 写入长期经验。

不适用于以下场景：

- 普通搜索结果排序问题。
- Provider 配置、向量库连接或数据库迁移失败。
- 用户手动删除记忆后的数据恢复。

这些问题应分别查看配置指南、搜索架构或数据库运维文档。

## 核心判定

系统经验回放效果只看结构化事实，不从原始 Prompt、原始 QA 或日志正文反推。

| 判定项 | 必要事实 | 合格标准 |
|--------|----------|----------|
| 有用召回 | `WasReplayed=true`、`Similarity`、`CitationFingerprint`、`SourceSessionId`、`ValueSignalKeyword`、`DistillationPromptFingerprint` | 相似度达到 `MemoryConstants.ChatPartialHitThreshold`，且引用事实完整 |
| 上下文污染风险 | `InjectedDynamicContext=true`、`ValueSignalKeyword`、引用事实 | 动态上下文缺少长期价值信号或引用事实不完整时必须标记风险 |
| 不可追踪复用风险 | `WasReplayed=true`、`CitationFingerprint`、`SourceSessionId`、`DistillationPromptFingerprint` | 直接命中或动态回放缺少来源事实时必须标记风险 |
| 低相似度复用 | `Similarity`、回放原因、引用事实 | 即使引用事实完整，低于阈值也不能算有用召回 |
| 未复用 | `WasReplayed=false` | 保持空评估，不应伪造召回收益 |

维护者应优先查看 `SystemExperienceReplayEvaluation` 生成的 `UsefulRecall`、`ContextPollutionRisk`、`UntraceableReuseRisk` 和 `EvaluationReason`。该评估只用于治理复盘，不改变现有回放决策。

## 验收矩阵

| 场景 | 触发方式 | 必看事实 | 通过标准 |
|------|----------|----------|----------|
| 可追踪动态回放 | 命中部分相似系统经验，并注入动态上下文 | `ReplayReason=dynamic-context`、`Similarity`、`MemoryCitation`、`ValueSignalKeyword` | `UsefulRecall=true`，无污染风险，无不可追踪风险 |
| 动态上下文缺少价值信号 | 回放标签缺少 `ValueSignalKeyword` | `InjectedDynamicContext=true`、`HasValueSignal=false` | `ContextPollutionRisk=true`，`EvaluationReason=dynamic-context-missing-value-signal` |
| 直接命中缺少来源事实 | direct answer 缺少来源会话或提纯 Prompt 指纹 | `ReplayReason=direct-answer`、`MemoryCitation` | `UntraceableReuseRisk=true`，不能把该命中当作可追踪收益 |
| 相似度低于阈值 | 引用事实完整但相似度不足 | `Similarity < ChatPartialHitThreshold` | `UsefulRecall=false`，`EvaluationReason=below-useful-threshold` |
| 未复用经验 | 没有命中或命中后未回放 | `WasReplayed=false` | `EvaluationReason=not-replayed`，不产生污染或不可追踪风险 |

验收时不要只看“是否命中经验”。命中但缺少引用事实、缺少价值信号或相似度不足，都不能证明记忆系统有效。

## 排查步骤

1. 从 `[AI.Memory.ReplayEvaluation]` 查找当前回合的 `TurnId`，确认真实回放链路已经产生评估事实。
2. 确认当前回合是否存在 `SystemExperienceReplaySnapshot`。
3. 查看回放方式：直接命中使用 `direct-answer`，动态上下文使用 `dynamic-context`。
4. 检查 `MemoryCitation` 是否包含 `CitationFingerprint`、来源会话、长期价值信号和提纯 Prompt 指纹。
5. 对照相似度阈值判断是否达到有用召回标准。
6. 若出现污染风险，检查动态上下文是否缺少价值信号或引用事实。
7. 若出现不可追踪复用风险，检查经验保存链路是否丢失来源会话、提纯协议或 Prompt 指纹。
8. 若召回收益无法证明，优先复核经验提纯准入和重复判重，而不是调整 Prompt 文案。

## 发布前检查

发布前至少确认：

- `SystemExperienceReplayEvaluationTests` 覆盖有用召回、污染风险、不可追踪风险、低相似度和未复用场景。
- 直接回放与动态上下文回放样例均能在 `[AI.Memory.ReplayEvaluation]` 找到对应结构化评估事实。
- 系统经验 SOP 不包含原始 QA、命令输出、临时 Debug 日志或一次性排查记录。
- 引用事实只保存结构化标识和指纹，不保存完整 Prompt 或原始对话正文。
- 复用经验的回合默认只观察，不重复提纯同一经验。
- 记忆效果评估不进入产品化审计表，不改变 `PromptCacheKey`，不反查数据库。

## 相关文档

- [Agent Runtime 架构](../02-architecture/06-agent-runtime-architecture.md)
- [测试与校验](../06-development/testing.md)
