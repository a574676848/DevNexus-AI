项目愿景与诉求说明书 (Project Vision Statement)

项目名称

DevNexus AI (智能研发工作站)

发起人

[Alex]

日期

2025-12-05

版本

v2.1 (Final - Full Spec)

1. 核心诉求 (Core Request)

构建一个企业级、私有化部署、全栈 .NET 的智能研发辅助平台。
服务端采用 .NET 10 + Aspire 构建高性能、可扩展的 AI 编排中枢；客户端采用 .NET MAUI + Blazor Hybrid 打造极具科技感和原生体验的桌面/移动应用。
系统彻底摒弃异构技术栈（Python），利用 .NET 原生能力实现自动化，并通过 SignalR 和 Block-Stream 协议 实现极致的实时交互体验。

2. 核心痛点与解决目标 (Problem & Solution)

痛点：

交互体验差：传统 ChatBot 只是文本流，无法打断、无状态同步、无法分屏预览代码。

开发效率低：前后端语言不通，DTO 重复定义，维护成本高。

数据不安全：公网 AI 代码泄露风险，Token 消耗无审计。

能力受限：普通 RAG 不懂代码结构，外部 Python 脚本部署复杂且不安全。

目标：

全栈复用：通过 DevNexus.Shared 实现前后端代码（DTO/Enum/Logic）100% 复用。

极致交互：利用 SignalR 实现全双工通信，支持思维链折叠、打断生成、实时终端日志。

原生智能：Native C# Agent。利用 Roslyn Scripting 在安全沙箱中执行 C# 代码，零依赖部署。

炫酷 UI：利用 Blazor Hybrid 实现无边框窗口、亚克力背景、Monaco Editor 代码分屏预览。

高可用与可观测：引入 Hangfire 异步处理重任务；引入 Redis 支撑实时消息分发；引入 Seq 实现 Token 审计与 AI 思考链路追踪。

3. 技术栈约束 (Tech Stack)

后端框架：.NET 10 (Preview) + .NET Aspire

前端框架：.NET MAUI + Blazor Hybrid (Razor Components)

通信协议：ASP.NET Core SignalR (Block-Stream Protocol)

共享库：DevNexus.Shared (.NET Standard 2.1 / .NET 10 Class Library)

AI 内核：Semantic Kernel (SK)

脚本引擎：Microsoft.DotNet.Interactive / Roslyn Scripting

任务队列：Hangfire (Postgres Storage)

基础设施 (IP: 192.168.10.99)：

Database: PostgreSQL (JSONB Support) - 存储业务数据与配置。

Vector DB: Qdrant - 存储 RAG 知识库向量。

Cache: Redis - 负责 SignalR 消息背板、对话上下文缓存、分布式锁。

Logging: Seq - 负责结构化日志聚合、AI 思考路径追踪、Token 消耗成本分析看板。

Runtime: Docker

存储：Local / 七牛云 / 阿里云 OSS

4. 关键功能场景 (Key Scenarios)

流式区块交互：服务端推送 BlockType.Thought（思考）-> BlockType.Code（代码）-> BlockType.Chart（图表），前端根据类型渲染不同组件。

分屏资产预览：AI 生成 HTML/Wiki 时，桌面端自动滑出右侧 Monaco Editor 预览窗口，支持实时 Diff 和高亮。

人机回环 (HITL)：敏感操作（如 SQL 执行）推送交互卡片，需用户点击“批准”后，服务端才继续运行。

实时 C# 终端：AI 编写并运行 C# 脚本，Console 输出通过 SignalR 实时推送到前端的“黑客帝国风格”终端窗口。

多模态分支对话：支持树状对话结构，允许用户基于某条历史消息创建新分支，支持图片/文件上下文。