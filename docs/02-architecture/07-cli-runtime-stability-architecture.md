# CLI Runtime 稳定性架构

## 1. 文档定位

本文档描述当前仓库中已经存在的 CLI Runtime / Agent Loop 稳定性主链。

## 2. 当前主链

当前 CLI Runtime 已经形成以下闭环：

1. 命令执行前统一进入 `CliExecutionPolicyService` 做工作目录、危险命令、审批与路径策略判断。
2. `HostService` 负责把一次命令请求接入 CLI 主链，并创建审批事件、checkpoint 与终端输出通知。
3. `ProcessCliRuntimeHost` 负责持久化 shell、输出缓冲、运行态快照、超时清理和会话终止。
4. `ICliRuntimeCoordinator` / `CliRuntimeCoordinator` 已作为统一协调入口，收口会话查询、日志分片、输入转发、等待终态、终止和回滚。
5. `ChatHub` 与 `ChatSessionRuntimeInspector` 已改为通过协调器消费 CLI 会话，而不是分别直连进程注册表与仓储。
6. `CliExecCheckpointService` 已覆盖高风险文件命令的快照创建、失效旧快照和显式回滚。
7. `ProcessCliRuntimeHost` 已具备可消费的 warm shell 预热与接管能力，`HostService` 创建会话前会先触发热池预热。
8. `ConfigurableCliSandboxSessionProvider` 已收口本地受限与容器隔离两类 sandbox，并具备 acquire / reuse / idle cleanup / orphan cleanup。
9. `TerminalOutputBuffer` / `TerminalStream` 的归档输出已经接入 CLI 日志读取主链，终端会话退出后仍可通过统一日志接口读取完整输出。
10. `AgentLoopRecoveryGuard` 已通过 `RuntimeRecoveryMiddleware`、`LoopGuardMiddleware` 和恢复管线把挂起交互拦截、不可恢复失败阻断与人工介入分流显式收口。

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

### 3.4 Infrastructure

Infrastructure 当前负责外部执行细节：

1. `CliRuntimeCoordinator`
2. `ProcessCliRuntimeHost`
3. `CliRuntimeWarmPool`
4. `ConfigurableCliSandboxSessionProvider`
5. `LocalRestrictedSandboxSessionProvider`
6. `ContainerSandboxSessionProvider`
7. `CliExecCheckpointService`

### 3.5 Client / Api

Client 与 Api 当前只消费统一状态和事件：

1. `ChatHub` 通过协调器暴露 `poll / wait / log / terminate / rollback`
2. `ChatContainer` 与终端面板消费统一运行态和日志结果
3. 统一事件流继续复用 `CliExecApprovalRequired`、`CliExecStarted`、`CliExecWaitingForInput`、`CliExecCompleted`、`CliExecFailed`、`CliExecCancelled`、`CliExecTimedOut`、`CliExecRolledBack`

## 4. 运行能力

### 4.1 统一协议与会话视图

以下协议已经是显式公共类型，而不再只是运行时拼装结果：

1. `CliExecApprovalRequestDto`
2. `CliExecSessionDto`
3. `CliExecLogChunkDto`
4. `CliExecCheckpointDto`
5. `CliApprovalStatus`

同时，`CliExecStatus` 与 `CliSessionState` 已补齐 `Queued` / `RolledBack` 这类细化状态值。

### 4.2 统一协调入口

CLI 主链已经显式拆成 `exec / process` 两层契约：

1. `ICliExecService` 负责接收执行请求
2. `ICliProcessService` / `ICliRuntimeCoordinator` 负责围绕既有会话做状态、日志、输入、终止和回滚

其中 `ICliRuntimeCoordinator` 当前承接以下 process 能力：

1. 会话状态查询
2. 日志分片读取
3. 等待终态
4. 输入转发
5. 终止
6. 回滚
7. 最近 checkpoint 查询

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
4. `AgentLoopRecoveryPipeline` 负责按顺序执行恢复中间件

### 4.5 输出治理

当前输出治理已接入统一日志事实源：

1. 运行中优先读取进程内存缓冲
2. 终端结束后通过 `TerminalOutputBuffer` / `TerminalStream` 读取归档输出
3. `CliExecSession` 已持久化最近关联的 `TerminalStreamId`
4. `GetLog / Poll / Wait` 会优先复用统一会话视图与归档事实，而不是只依赖进程内存缓冲

## 5. 当前收益

当前实现已经直接改善以下问题：

1. 终端状态读取不再散落在 Hub、RuntimeInspector 和 HostService 多处重复拼装。
2. 审批、日志、终止、回滚、等待终态已经有统一会话视图和稳定 DTO。
3. 高风险命令具备显式 checkpoint / rollback 主链。
4. Agent Loop 的恢复判断不再只是隐式代码路径，而是可替换的 recovery middleware。
5. sandbox 生命周期和运行态协调职责已经基本从单一宿主类中拆开。

## 6. 当前结论

当前 CLI Runtime 主链具备以下结构：

1. `exec / process` 已有显式公共契约
2. `Queued / RolledBack` 已有统一状态值
3. warm pool 已升级为可消费的热 shell
4. CLI 日志读取已接入归档事实源
5. `CliExecutionRuntimeMapper` 已退化为 API 适配器，DTO 主映射收口到 Core

CLI Runtime 的基础架构边界由上述契约、协调器、Sandbox 生命周期和归档日志事实源共同定义。
