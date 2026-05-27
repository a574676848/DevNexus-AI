# Agent Loop 与 CLI 运行故障处理指南

本文档面向维护者和现场排障人员，说明聊天 Agent Loop、CLI 终端会话和 Swarm 工作包中长命令、等待输入、停止、日志轮询与回滚的处理路径。

## 适用范围

适用于以下场景：

- 聊天中模型已经调用终端命令，但界面显示仍在运行。
- CLI 命令等待 stdin，例如确认提示、密码提示或交互式安装。
- 用户需要停止卡住的终端命令。
- 终端面板或日志轮询没有显示最新输出。
- Swarm 工作包内的命令需要继续等待、发送输入或停止。
- 高风险文件命令需要查看 checkpoint 或执行回滚。

不适用于以下场景：

- LLM Provider 配置错误。
- 文件上传、语义解析或 FileTask runner 的独立失败。
- 数据库、Redis、Qdrant、Elasticsearch 的通用部署故障。

这些问题分别查看配置指南、用户指南和 FAQ。

## 快速判断

| 现象 | 优先检查 | 正确处理 |
|------|----------|----------|
| 命令仍在运行 | `StatusSummary`、`recommendedTool`、`CliExecStatus.Running` | 继续等待同一会话，不要重新执行命令 |
| 命令等待输入 | `waitingForInput=true`、`nextAction=SendInput`、`recommendedTool=HostService.SendCommandInputAsync` | 发送 stdin 到同一会话 |
| 停止未完成 | `recommendedTool=HostService.StopCommandAsync` | 继续停止同一会话 |
| 终端日志不刷新 | `/chat-hub`、`GetCliExecLog(sessionId,startIndex)`、`CliExecOutputUpdated` | 先确认 SignalR 与日志分块，再看归档输出 |
| 会话已经结束但 UI 仍显示运行 | `CliExecSession` 持久化状态、终端归档、SignalR 事件 | 以协调器返回的会话状态为准 |
| 回滚按钮不可用 | 会话是否仍活跃、是否存在 active checkpoint | 活跃会话不能回滚，先停止或等待终态 |
| Swarm 工作包卡住 | 工作包状态、会话 `StatusSummary`、Host 工具返回文本 | 使用 Swarm Host 续接工具，不直接操作进程注册表 |
| 命令异常退出 | `ExecStatus=Failed`、`TerminationReason=ProcessExited`、退出码和尾部输出 | 先复盘输出和工作目录状态，不默认执行回滚 |

## 正常链路

CLI 主链路按以下顺序工作：

1. 用户或模型发起命令。
2. `HostService` 进入 CLI 执行策略，按真实本机路径解析工作目录，必要时创建审批或 checkpoint。
3. `ProcessCliRuntimeHost` 创建或复用持久化 shell。
4. 输出进入进程内缓冲，并由终端归档链路写入事实源。
5. `CliRuntimeCoordinator` 统一提供会话状态、日志分块、stdin、等待终态、停止和回滚。
6. `ChatHub` 通过 `/chat-hub` 暴露 `GetCliExecLog`、`WaitCliExecSession`、`SendCliInput`、`TerminateCliSession` 和 `RollbackCliExecSession`。
7. 前端终端面板消费 `StatusSummary`、日志分块和实时事件。
8. Agent Loop 根据工具结果中的 `ToolSuggestedAction` 和 `recommendedTool` 选择继续等待、发送输入、停止或总结。

维护时不要绕过 `ICliRuntimeCoordinator` 直接操作 `ICliProcessRegistry`。普通 Agent Loop 和 Swarm 工作包都应走同一协调器入口，避免状态、日志和持久化事实分叉。

后台清理任务只允许清理平台内部过期资源，不应扫描或删除用户显式传入的 CLI `workingDirectory`。

## 状态与下一步动作

排障时优先看结构化字段，不解析中文文案：

- `CliSessionStateDto.ExecStatus`
- `CliSessionStateDto.WaitingForInput`
- `CliSessionStateDto.TerminationReason`
- `CliSessionStateDto.StatusSummary`
- 工具返回文本中的 `recommendedTool`
- 工具执行记录中的 `ToolSuggestedAction`

常见映射：

| 状态事实 | Agent Loop 动作 | 下一步 |
|----------|-----------------|--------|
| `recommendedTool=HostService.WaitCommandAsync` | `WaitForCompletion` | 调用等待工具读取同一会话 |
| `recommendedTool=HostService.SendCommandInputAsync` | `PromptUserInput` | 调用 stdin 工具发送输入 |
| `recommendedTool=HostService.StopCommandAsync` | `StopCommand` | 调用停止工具继续终止同一会话 |
| 终态会话 | `ReviewResult` | 总结输出或检查结果 |

CLI stdin 续接不是产品化挂起交互。只有底层工具明确标记 `RequiresHumanIntervention` 时才进入挂起交互，否则应继续同一终端会话。

## 日志轮询排查

终端日志有两个来源：

- live 输出：来自进程内保留缓冲。
- archived 输出：来自 `TerminalOutputBuffer` / `TerminalStream` 归档事实源。

排查顺序：

1. 确认 `/chat-hub` 已连接。
2. 调用或观察 `GetCliExecLog(sessionId, startIndex)` 返回值。
3. 若会话仍活跃，live 输出已经由进程注册表按 `startIndex` 返回增量，协调器不得再次切片。
4. 若会话已终态，归档输出是完整事实源，应按 `startIndex` 返回增量。
5. 若输出以裁剪标记开头，说明进程内缓冲触发内存水位裁剪，应保留当前窗口和裁剪标记，而不是认为没有新输出。
6. 并发 session 不共享偏移量。live 会话和 archived 会话必须分别按各自来源处理 `startIndex`，不能把一个会话的 `_lastPolledOutputLength` 套到另一个会话。
7. 若 REST 可返回日志但 UI 不刷新，优先检查 SignalR 订阅和终端面板的 `_lastPolledOutputLength` 更新路径。

不要把模型可见预览当成完整日志事实源。模型预览只服务 Agent Loop 上下文预算，完整日志以归档事实源和日志分块接口为准。

## 真实场景验收矩阵

以下矩阵用于补齐 CLI 稳定性验收。每个场景都应保留会话 ID、命令、关键状态字段、日志分块返回和最终处理动作，方便复盘是否仍沿用统一协调器主链。

| 场景 | 建议触发方式 | 必看事实 | 通过标准 |
|------|--------------|----------|----------|
| 长进程仍在运行 | 执行持续输出或长时间构建命令 | `ExecStatus=Running`、`StatusSummary.NextAction=WatchOutput`、`recommendedTool=HostService.WaitCommandAsync` | Agent Loop 继续等待同一会话；终端面板持续轮询日志；不会重新执行相同命令 |
| 交互命令等待输入 | 执行需要确认、项目名或密码提示的命令 | `WaitingForInput=true`、`StatusSummary.RequiresInput=true`、中文或英文 stdin 提示 | 输入框切换为 stdin；输入通过 `SendCliInput` 或 `SendCommandInputAsync` 写入同一会话；空输入保持空行语义 |
| 普通异常退出 | 执行会返回非零退出码的命令 | `ExecStatus=Failed`、`TerminationReason=ProcessExited`、退出码、尾部输出 | 下一步默认为 `ReviewResult`；先复盘输出和工作目录状态；没有 active checkpoint 时不显示可回滚假象 |
| 中文编码与控制字符 | 输出中文、ANSI 颜色和进度控制字符 | 清洗后的中文正文、等待输入识别结果、模型可见预览 | ANSI 与非文本控制字符被清理；中文正文和中文等待输入提示不丢失 |
| 超大输出与缓冲裁剪 | 执行大量输出命令或长日志命令 | `OutputLength`、裁剪标记、live / archived 来源、尾部输出 | 旧偏移越界时仍返回当前保留窗口和裁剪标记；模型预览只给头尾窗口，完整日志仍以归档为准 |
| 并发 session 日志隔离 | 同时启动两个 CLI 会话并分别轮询 | 每个 `sessionId` 的 `startIndex`、`OutputLength`、`PlainOutput` | live 与 archived 来源互不串用；不同 session 不共享 `_lastPolledOutputLength`；日志不会串线 |
| Swarm 工作包续接 | 在 Swarm 工作包内触发等待、stdin 或停止 | 工作包状态、会话 `StatusSummary`、`SwarmHostPlugin` 返回文本 | Swarm 与普通聊天使用同一 `ICliRuntimeCoordinator`；下一步动作与普通聊天一致 |

验收时不要只看 UI 是否显示成功。若 `recommendedTool`、`StatusSummary.NextAction`、`ExecStatus` 或日志分块来源之间出现矛盾，应优先按协调器状态和归档事实源排查，再判断是否需要修复 UI 展示或 Agent Loop 恢复策略。

## 等待、输入与停止

### 命令仍在运行

正确动作：

1. 保持当前 CLI 会话。
2. 使用 `WaitCommandAsync` 或 `WaitCliExecSession` 继续等待。
3. 根据最新 `StatusSummary` 判断是否转为 stdin、停止或总结。

不要重新调用 `HostService.ExecuteCommandAsync` 执行相同命令。重复执行会造成并发会话、重复副作用和日志事实混乱。

### 命令等待 stdin

正确动作：

1. 确认状态中存在 `WaitingForInput` 或 `recommendedTool=HostService.SendCommandInputAsync`。
2. 通过 `SendCommandInputAsync` 或 `SendCliInput` 发送输入。
3. 空输入是合法 stdin 空行，不应被拦截。
4. 发送后继续等待同一会话状态变化。

输入内容会经过 `CliRuntimeInputProtocol` 规范化，避免前端、Hub 和宿主重复追加换行。

等待输入识别会先清理 ANSI 和非文本控制字符，再识别常见英文和中文提示。中文场景包括 `是否继续？[y/n]`、`请输入项目名称：`、`请确认删除操作:` 这类本地化交互提示。中文输出属于合法文本，不应在清洗过程中丢失。

### 停止卡住命令

正确动作：

1. 通过 `StopCommandAsync` 或 `TerminateCliSession` 停止当前会话。
2. 如果停止返回后会话仍活跃，继续停止同一会话。
3. 如果会话已经终态，转为总结或检查结果。
4. 如果会话缺失，返回失败并推荐 `ReviewResult`，不能伪装成已成功停止。

Agent Loop 连续停止未闭环时由 loop guard 控制预算，避免无限停止循环。

### 命令异常退出

异常退出通常表现为非零退出码，并持久化为 `ExecStatus=Failed` 与 `TerminationReason=ProcessExited`。这类失败不等同于文件变更需要回滚，默认下一步应是 `ReviewResult`：

1. 查看退出码、尾部输出和当前工作目录状态。
2. 判断失败是否来自命令参数、依赖缺失、测试失败或外部进程自身错误。
3. 只有存在 active checkpoint 且命令属于高风险文件变更时，才进入回滚流程。
4. 没有 checkpoint 时，不应把异常退出伪装成可回滚状态。

## 回滚排查

回滚只适用于存在 active checkpoint 的高风险文件命令。

处理顺序：

1. 先确认会话不是活跃状态。运行中的会话不能回滚。
2. 查看当前会话是否存在 `LatestCheckpoint`。
3. 通过 `RollbackCliExecSession` 进入协调器回滚。
4. 回滚成功后，事件与返回值都应携带统一 `State`。
5. 后续轮询应看到 `CliExecStatus.RolledBack` 或对应终态摘要。

不要手工恢复文件后再让平台状态保持运行中。文件事实、checkpoint 状态和会话状态必须一起闭环。

## Swarm 工作包中的 CLI 会话

Swarm 工作包中的 CLI 续接与普通聊天一致：

- 等待使用 `SwarmHostPlugin.WaitCommandAsync`。
- stdin 使用 `SwarmHostPlugin.SendCommandInputAsync`。
- 停止使用 `SwarmHostPlugin.StopCommandAsync`。
- 三者都进入 `ICliRuntimeCoordinator`。

排查 Swarm 卡住时，先看工作包状态和会话 `StatusSummary`，再看 CLI 日志。不要让 Swarm 工作包绕过协调器直接操作进程注册表。

## 观测入口

当前结构化日志会按事件归入主题：

- `AgentLoop`
- `ToolExecution`
- `Terminal`
- `MessageGeneration`
- `SessionRecovery`
- `ThinkingChain`

排查建议：

1. 先用会话 ID、消息 ID 或终端 session ID 过滤。
2. Agent Loop 问题看 `AgentLoopEvaluationStarted`、`AgentLoopEvaluationCompleted`、`AgentLoopRepairDecided`、`AgentLoopRepairAttemptStarted` 和 `AgentLoopMaxAttemptsReached`。
3. 工具执行问题看 `ToolExecutionStarted`、`ToolExecutionCompleted`、`ToolExecutionFailed` 和 `ToolExecutionTimeout`。
4. 终端归档和回放问题看 `TerminalStreamStarted`、`TerminalStreamChunkReceived`、`TerminalStreamCompleted`、`TerminalPersistenceCompleted`、`TerminalPersistenceFailed` 和 `TerminalReplayStarted`。
5. Prompt 缓存标记、动态上下文和成本诊断进入 `[AI.Prompt.Diagnostics]` 结构化日志；重点字段包括 `NonCachedInputTokens`、`CacheHitRatio`、`DynamicContextRatio` 和 `HistoryRatio`，不进入产品化审计表。

## 升级为代码问题的标准

满足以下任一条件时，应记录为代码缺陷，而不是让用户重试：

- live 日志非零 `startIndex` 返回空 chunk，但终端仍有新输出。
- 并发 session 日志串线，或 live / archived 会话复用了错误的 `startIndex`。
- `recommendedTool` 与 `StatusSummary.NextAction` 明显矛盾。
- CLI stdin 被升级为产品化挂起交互。
- `StopCommand` 返回仍活跃，但 Agent Loop 改为重新执行命令。
- 终态会话仍允许回滚以外的运行中操作。
- Swarm 工作包与普通聊天对同一 CLI 状态给出不同下一步动作。

记录缺陷时至少包含：

- `chatSessionId`
- `messageId`
- `cli sessionId`
- 当前 `ExecStatus`
- `StatusSummary`
- 最近一次工具返回文本
- `GetCliExecLog(sessionId,startIndex)` 的输入和输出长度
- 相关 SignalR 事件类型

## 相关文档

- [用户指南](./user-guide.md)
- [配置指南](./configuration-guide.md)
- [CLI Runtime 稳定性架构](../02-architecture/07-cli-runtime-stability-architecture.md)
- [Agent Runtime 架构](../02-architecture/06-agent-runtime-architecture.md)
- [测试与校验](../06-development/testing.md)
