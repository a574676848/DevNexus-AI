# Prompt 缓存成本诊断指南

本文档面向维护者和现场排障人员，说明如何观察 Prompt 缓存命中、非缓存输入成本、动态上下文占比和历史消息占比。

## 适用范围

适用于以下场景：

- 需要判断一次真实请求是否命中 Prompt 缓存。
- 需要估算未命中缓存的输入 Token 成本。
- 需要确认动态上下文是否挤占了稳定前缀。
- 需要排查历史消息是否导致上下文成本上升。
- 需要确认 Prompt/cache 诊断没有进入公开审计表、公开 API 或主界面。

不适用于以下场景：

- 结算账单对账。
- Provider 原生缓存 Header 能力验证。
- PromptCacheKey 结构变更评审。
- 用户主界面展示指标设计。

## 观测入口

Prompt/cache 成本诊断只进入结构化日志：

```text
[AI.Prompt.Diagnostics]
```

日志由 `TokenAuditFilter` 和 `TokenAuditService` 在函数调用与流式完成两条路径写入。维护者应使用 `SessionId`、`MessageId`、`InvocationKind` 和 `PromptCacheKey` 过滤日志，不要从产品化审计表反查这些字段。

## 核心字段

| 字段 | 含义 | 判读 |
|------|------|------|
| `CachedPromptTokens` | Provider 返回的缓存命中 Prompt Token 数 | 为空表示 Provider 没有提供该观测值 |
| `NonCachedInputTokens` | 未命中缓存的输入 Token 数 | 越高说明本次请求需要重新计费或重新处理的输入越多 |
| `CacheHitRatio` | 缓存命中输入占比 | 接近 1 表示大部分输入命中缓存，接近 0 表示缓存收益低 |
| `DynamicContextRatio` | 动态上下文占输入 Token 比例 | 过高时优先检查检索结果、文件摘要和临时上下文 |
| `HistoryRatio` | 历史消息占输入 Token 比例 | 过高时优先检查会话压缩、历史裁剪和长对话策略 |
| `StablePrefixHash` | 稳定前缀指纹 | 用于判断稳定前缀是否漂移 |
| `ToolSchemaHash` | 工具 Schema 指纹 | 用于判断工具目录变化是否导致缓存边界变化 |

`PromptCostDiagnostics` 会把负数 Token 观测值归一化为 0，并把比例计算限制在 0 到 1。该归一化只服务诊断日志和离线排查，不修改产品化审计口径。

## 快速判断

| 现象 | 优先检查 | 正确处理 |
|------|----------|----------|
| 缓存命中率突然下降 | `CacheHitRatio`、`StablePrefixHash`、`ToolSchemaHash` | 先判断稳定前缀或工具 Schema 是否变化 |
| 输入成本突然升高 | `NonCachedInputTokens`、`InputTokens`、`CachedPromptTokens` | 确认 Provider 是否返回缓存 Token，再看动态上下文 |
| 动态上下文占比过高 | `DynamicContextRatio`、`DynamicContextTokens` | 检查检索结果数量、文件摘要和临时上下文注入 |
| 历史消息占比过高 | `HistoryRatio`、`HistoryTokens` | 检查长会话裁剪、摘要压缩和历史回放策略 |
| 主界面出现 Prompt 成本指标 | UI 文案、审计 DTO、公开 API | 回退展示，保留在 `[AI.Prompt.Diagnostics]` |

## 验收矩阵

| 场景 | 必看事实 | 通过标准 |
|------|----------|----------|
| 函数调用路径 | `InvocationKind`、`NonCachedInputTokens`、`CacheHitRatio` | 诊断日志存在，产品化审计表不新增字段 |
| 流式完成路径 | `SessionId`、`MessageId`、`DynamicContextRatio`、`HistoryRatio` | 诊断日志能关联会话和消息 |
| Provider 未返回缓存 Token | `CachedPromptTokens=null` | `CacheHitRatio` 可为空，不伪造缓存命中 |
| Provider 返回异常 Token | 负数或超过输入 Token 的观测值 | 诊断组件归一化，不污染 `TokenUsageMetrics` |
| 工具目录变化 | `ToolSchemaHash` 变化 | 解释为缓存边界变化，不误判为 Provider 异常 |

## 不进入公开契约的内容

以下内容不得进入普通用户主界面、公开审计 API、公开 DTO 或数据库审计表：

1. `CacheHitRatio`
2. `DynamicContextRatio`
3. `HistoryRatio`
4. `StablePrefixHash`
5. `ToolSchemaHash`
6. `StablePrefixManifest`
7. `DynamicContextManifest`

只有当指标形成明确用户价值、字段语义稳定且经过 API 契约评审后，才能考虑进入公开接口。

## 相关文档

- [Agent Loop 与 CLI 运行故障处理指南](./agent-cli-operations.md)
- [用户指南](./user-guide.md)
- [测试与校验](../06-development/testing.md)
- [客户端 UI 设计规范](../05-design/01-client-ui-design.md)
