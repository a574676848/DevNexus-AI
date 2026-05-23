# 测试与校验

本文档只说明当前仓库可直接采用的校验方式。

## 测试覆盖

自动化测试位于 `src/tests/DevNexus.Core.Tests`，覆盖 Core 层纯逻辑，不依赖 Infrastructure、数据库或 UI。主要覆盖领域包括：

- 工具调用序列验证、截断恢复、参数预验证、Schema 指纹
- CLI 运行时会话管理、命令哨兵协议、stdin 输入、中文交互提示识别、终端输出清洗与预览
- Agent Loop 恢复策略、运行时续接、完成判定、停止协议
- Agent Loop 长目标、多轮恢复、多 Provider 切换与 Provider 超时降级路径
- Prompt 缓存标记、片段清单、漂移分析、Token 指标
- Swarm 会话状态摘要、取消/失败收尾、工作包生命周期
- 事件批次诊断、工具执行耗时与失败分析
- 挂起交互状态摘要与恢复上下文

## 当前校验方式

### 1. 先做工作树门禁

```powershell
.\tools\verify-worktree.ps1
```

该脚本会覆盖已跟踪差异和未跟踪新增文件，检查 `git diff --check`、UTF-8 BOM、700 行上限、未跟踪文本文件尾随空白和合并冲突标记。

单个提交候选已经暂存后，可只验证暂存区：

```powershell
.\tools\verify-worktree.ps1 -Scope Staged
```

`-Scope Staged` 用于提交候选自检；发布前最终收敛仍应运行默认 `All` 范围，避免未暂存文件或新增文件逃过检查。

提交候选进入评审前，还应确认暂存区只包含一个产品批次：

```powershell
.\tools\suggest-change-batches.ps1 -Scope Staged -RequireSingleBatch -OutputMarkdown
```

该命令会在暂存区为空、混入多个批次或存在未归类文件时失败。

暂存区提交候选也可以使用一键自检命令：

```powershell
.\tools\verify-staged-batch.ps1
```

该脚本会先验证暂存区只包含一个产品批次，再执行暂存区工作树门禁。
通过后会输出暂存文件数、批次名称、门禁结果和下一步证据记录要求，方便维护者补齐 GitNexus、场景证据、验证结果和回滚计划。
验证指定提交候选时，可传入 `-BatchName "Prompt"`，确保暂存区不是误选的其他产品批次。`suggest-change-batches.ps1 -OutputMarkdown` 会在每个验收骨架中输出带批次名的建议验证命令和目标暂存区门禁命令，提交前应优先使用该命令。
如果暂存批次对应文件仍存在未暂存改动，`suggest-change-batches.ps1 -Scope Staged -OutputMarkdown` 会输出 `Staged boundary: incomplete`，且 `verify-staged-batch.ps1` 默认失败。维护者需要继续暂存相关改动；确实要拆分 hunk 时，显式传入 `-AllowPartialFileScope`，并在提交候选说明中记录拆分理由和剩余改动归属。

### 2. 再恢复并构建

```bash
dotnet restore src/DevNexus.sln
dotnet build src/DevNexus.sln
```

### 3. 运行自动化测试

```bash
dotnet test src/DevNexus.sln --no-build
```

### 4. 再做主流程冒烟验证

至少验证以下流程：

1. 登录与聊天流式输出
2. 文件上传与解析状态回传
3. 文件任务创建与结果回灌
4. 设置页中的系统信息、更新检查和发布中心

### 5. 产品化真实场景验证矩阵

涉及运行态、UI 或跨组件链路的提交候选，至少补齐下表中的对应证据，并把结果写入 PR 描述。

| 场景 | 必看路径 | 通过标准 | 证据 |
|------|----------|----------|------|
| Agent Loop 长会话恢复 | 长目标、多工具混合失败、Provider 超时、CLI 等待、stdin 续接、停止未闭环 | 自动修复能选择确定性恢复；协调入口能生成内部续接消息或低噪停止，不空转 | `AgentLoopExecutorTests` / `ChatAgentLoopCoordinatorTests`、真实长会话记录、GitNexus `StreamMessageAsync` 风险归因 |
| CLI/终端真实运行 | 长进程、交互命令、异常退出、中文输出、超大输出、并发 session | 状态、输出预览、归档日志和下一步动作一致；live 与 archived 日志分块不串源 | Terminal / CLI 定向测试、手动命令记录、`GetCliExecLog` 风险归因 |
| Prompt/cache 成本诊断 | 函数调用、流式完成、Provider 未返回缓存 Token、动态上下文升高 | `[AI.Prompt.Diagnostics]` 有 `NonCachedInputTokens`、`CacheHitRatio`、`DynamicContextRatio`、`HistoryRatio`；公开审计契约不新增字段 | Prompt 诊断测试、结构化日志样例、公开 API/DTO 排除说明 |
| 记忆回放效果 | 有用召回、低相似度、污染风险、不可追踪复用、未复用 | `[AI.Memory.ReplayEvaluation]` 能区分有用召回与风险；低价值或不可追踪内容不能放大进动态上下文 | `SystemExperienceReplayEvaluation` 测试、结构化日志样例、记忆治理文档链接 |
| Swarm 收口 | 工作包失败、运行中阻塞、终态结果、执行报告 Artifact、缺少结果证据 | `[AI.Swarm.Review]` 能记录可恢复性、阻塞与复盘证据；用户能看到必要动作且主聊天保持低噪 | Swarm 定向测试、结构化日志样例、终态/非终态与 UI 文档链接 |
| 产品体验与文档 | 聊天、终端、Swarm、审计、FAQ、用户指南、维护者诊断 | 用户路径能从主界面进入终端状态和复盘入口；内部诊断不进入普通主界面 | 用户指南、FAQ、docs 导航、发布门禁结果 |

### 6. 涉及数据库或迁移时

额外确认：

- 应用能正常启动
- 迁移能应用成功
- 相关页面和接口能返回预期结果

## 测试边界

正式测试项目以仓库实际目录为准。没有测试项目覆盖的改动，需要在提交说明中写明手动冒烟验证范围。


至少验证以下流程：

1. 登录与聊天流式输出
2. 文件上传与解析状态回传
3. 文件任务创建与结果回灌
4. 设置页中的系统信息、更新检查和发布中心

### 5. 产品化真实场景验证矩阵

涉及运行态、UI 或跨组件链路的提交候选，至少补齐下表中的对应证据，并把结果写入 PR 描述。

| 场景 | 必看路径 | 通过标准 | 证据 |
|------|----------|----------|------|
| Agent Loop 长会话恢复 | 长目标、多工具混合失败、Provider 超时、CLI 等待、stdin 续接、停止未闭环 | 自动修复能选择确定性恢复；协调入口能生成内部续接消息或低噪停止，不空转 | `AgentLoopExecutorTests` / `ChatAgentLoopCoordinatorTests`、真实长会话记录 |
| CLI/终端真实运行 | 长进程、交互命令、异常退出、中文输出、超大输出、并发 session | 状态、输出预览、归档日志和下一步动作一致；live 与 archived 日志分块不串源 | Terminal / CLI 定向测试、手动命令记录 |
| Prompt/cache 成本诊断 | 函数调用、流式完成、Provider 未返回缓存 Token、动态上下文升高 | `[AI.Prompt.Diagnostics]` 有 `NonCachedInputTokens`、`CacheHitRatio`、`DynamicContextRatio`；公开审计契约不新增字段 | Prompt 诊断测试、结构化日志样例 |
| 记忆回放效果 | 有用召回、低相似度、污染风险、不可追踪复用、未复用 | `[AI.Memory.ReplayEvaluation]` 能区分有用召回与风险；低价值内容不能放大进动态上下文 | `SystemExperienceReplayEvaluation` 测试、结构化日志样例 |
| Swarm 收口 | 工作包失败、运行中阻塞、终态结果、执行报告 Artifact、缺少结果证据 | `[AI.Swarm.Review]` 能记录可恢复性、阻塞与复盘证据；主聊天保持低噪 | Swarm 定向测试、结构化日志样例 |
| 产品体验与文档 | 聊天、终端、Swarm、审计、FAQ、用户指南、维护者诊断 | 用户路径能从主界面进入终端状态和复盘入口；内部诊断不进入普通主界面 | 用户指南、FAQ、docs 导航、发布门禁结果 |

### 6. 涉及数据库或迁移时

额外确认：

- 应用能正常启动
- 迁移能应用成功
- 相关页面和接口能返回预期结果

## 测试边界

正式测试项目以仓库实际目录为准。没有测试项目覆盖的改动，需要在提交说明中写明手动冒烟验证范围。
