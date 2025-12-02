30天落地行动计划 (Action Plan)
📅 Week 1: 基础设施与骨架 (Foundation)
Day 1: 创建 .NET 10 Aspire 项目。

配置 AppHost 连接字符串指向 192.168.10.99 的 PG, Qdrant 和 Seq。

Day 2-3: 数据库建模与迁移。

建立 User, Config, ChatHistory 表。验证 EF Core 远程连接。

Day 4-5: Fluent UI Shell 搭建。

实现毛玻璃侧边栏、顶部栏。

开发 沉浸式登录页，跑通 Identity 认证。

📅 Week 2: 控制枢纽与 AI 核心 (The Brain)
Day 6-8: 开发 Control Hub。

实现 AI、Jira、GitLab 的配置保存与 "Test Connection" 逻辑。

Day 9-10: Semantic Kernel 集成。

实现 /api/chat 流式接口。

跑通 "Hello World" (UI 发送 -> SK 处理 -> UI 显示)。

📅 Week 3: 交互引擎攻坚 (The Artifacts)
Day 11-12: 实现 Widget Factory。

前端解析 JSON 流，动态加载 Razor 组件。

Day 13-14: 集成 Monaco Editor。

开发 SQL Runner 和 Code Diff 组件。

Day 15: 本地文件系统访问。

实现后端读取本地文件的 API，打通 "Apply Changes" 功能。

📅 Week 4: 集成与打磨 (Integration & Polish)
Day 16-18: 外部 API 对接。

实现 Jira Plugin (查任务、改状态)。

实现 GitLab Plugin (查代码)。

Day 19: 本地代码 RAG。

编写后台任务，扫描本地文件夹 -> 存入远程 Qdrant。

Day 20: 视觉与动画打磨。

添加进场动画，优化 Dark Mode，发布 v1.0。