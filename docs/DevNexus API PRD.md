服务端产品需求文档 (PRD) - DevNexus API

文档类型

服务端 API 规格说明书

项目阶段

Phase 1 (Backend Core)

版本

v2.1 (Final - Full Spec)

适用对象

后端开发, 架构师

1. 系统架构概述

采用 Pure .NET Modular Monolith 架构。核心通信层升级为 SignalR，业务逻辑层依赖 Roslyn 动态编译，基础设施层深度集成 Redis 与 Seq 以保障高性能与可观测性。

Host: DevNexus.AppHost (Aspire Orchestrator)

API: DevNexus.API (SignalR Hubs + REST API)

Worker: DevNexus.Worker (Hangfire Server)

Shared: DevNexus.Shared (DTOs, Enums, Validators - 前后端共用)

2. 核心功能模块

2.1 鉴权与用户中心 (Identity & Auth)

基础功能: Login/Register (基于 ASP.NET Identity)。

客户端适配:

RefreshToken: 支持长效 Token (30天滑动过期)，适应 App 长期在线需求。

DeviceId: 登录接口记录设备指纹，支持多端状态同步。

版本管理: 提供 /api/system/client-version 接口，支持 MAUI 客户端检查更新和下载安装包。

2.2 基础设施深度集成 (Infrastructure)

Redis (Cache & Backplane):

SignalR Backplane: 配置 Redis 为 SignalR 背板，确保未来多实例扩展时消息能正确广播到所有客户端。

Context Cache: 缓存活跃会话的最近 20 条消息上下文，减少 Postgres IO 压力，降低首字延迟。

Distributed Lock: 在生成 Wiki 或执行全局任务时使用 Redis 分布式锁，防止并发冲突。

Seq (Observability):

Token Audit Dashboard: 记录每次 LLM 调用的 InputTokens, OutputTokens, ModelId，在 Seq 中构建实时成本看板。

AI Tracing: 记录 Semantic Kernel 的 FunctionInvoking 和 FunctionInvoked 事件，完整可视化 AI 的思考路径和插件调用链。

Exception Alerting: 聚合系统异常（如脚本执行失败、OSS 上传超时），提供即时告警。

2.3 交互协议定义 (DevNexus.Shared)

Block Types (枚举):

TextDelta: 普通 Markdown 增量。

ThoughtChain: 思考过程 (前端渲染为折叠面板)。

ArtifactStart/Delta/End: 独立文档 (触发分屏预览)。

InteractiveCard: SqlApproval (审批), ScriptRunner (终端), WebSearch (引用)。

Chart: Plotly JSON 数据。

SignalR Event: ReceiveBlock(ServerEvent evt)。

2.4 实时通信内核 (Real-time Core)

ChatHub:

SendMessage(ChatRequest payload): 发送消息。

StopGeneration(Guid sessionId): 打断机制，取消 CancellationToken。

ApproveAction/RejectAction: 人机回环控制。

状态同步: 任意一端发送消息，SignalR 需向该 User 的所有 ConnectionId 广播，实现“桌面端发，手机端看”。

2.5 数据模型 (Data Model)

ChatMessages 表:

Id (Guid)

ParentId (Guid?): 支持树状对话。

Content (JSONB): 支持多模态混排 (Text + ImageUrl + FileRef)。

Artifacts 表:

Type: Html, CSharp, Python, Markdown, Pdf。

ParentArtifactId: 支持版本控制。

2.6 C# 原生脚本引擎 (Native Scripting Engine)

核心: Roslyn Scripting / DotNet Interactive。

Live Console: 重定向 Console.Out，通过 SignalR 实时推送 RunnerPayload 到前端。

安全熔断:

Timeout: 强制 30s 超时。

Isolation: 推荐 Docker API 启动临时容器，或受限 AssemblyLoadContext。

2.7 任务队列与资产管理

Hangfire: 处理 PDF 生成、OSS 上传、过期文件清理。

Storage: IFileStorageService 支持 Local/Qiniu/Aliyun 切换。

2.8 插件体系 (Plugins)

CodeAwareRAG: 基于 Roslyn 语法树解析代码结构（Class/Method 切分）。

WebSearch: Bing 搜索封装。

DocumentMaker: 异步生成 HTML/PDF。