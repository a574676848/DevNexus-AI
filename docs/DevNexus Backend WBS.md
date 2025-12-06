服务端工作分解结构 (WBS) - DevNexus Backend

周期预估

5 周 (25 人天)

版本

v2.1 (Full Spec)

Phase 1: 基础设施与共享内核 (Week 1)

目标: 搭建 Aspire 骨架，确立前后端契约，配置 Redis/Seq。

ID

任务名称

描述

估时 (天)

优先级

1.1

项目初始化

创建 Aspire 解决方案

0.5

P0

1.2

Shared 库开发

关键: 创建 DevNexus.Shared，定义 BlockType, DTOs

1.5

P0

1.3

EF Core 建模

User, Artifact, ChatMessage (JSONB, Tree)

1.0

P0

1.4

Redis & Seq 集成

配置 Redis 为 SignalR 背板，配置 Serilog Sink 到 Seq

0.5

P0

1.5

Auth & Update

Identity, RefreshToken, Version API

1.5

P0

M1

里程碑

基础设施就绪，共享库定义完成，日志与缓存打通

-

-

Phase 2: 实时通信内核 (Week 2)

目标: SignalR 全双工 + Kernel 适配。

ID

任务名称

描述

估时 (天)

优先级

2.1

ChatHub 开发

Send/Stop/Approve 接口，多端广播逻辑

1.5

P0

2.2

Kernel 适配器

LLM 流 -> BlockStream 转换器，集成 Seq Tracing

2.0

P0

2.3

Token 审计

实现 SK Filter，记录 Token 使用量到 Seq

1.0

P0

2.4

打断与审批

CancellationTokenSource 管理

1.0

P0

M2

里程碑

支持打断生成，Seq 中可看到 Token 消耗

-

-

Phase 3: 资产、存储与可视化 (Week 3)

目标: OSS, Artifacts, Charts.

ID

任务名称

描述

估时 (天)

优先级

3.1

OSS 封装

IFileStorageService (Local/Qiniu/Aliyun)

1.0

P0

3.2

Artifact 推流

识别代码块，推送 ArtifactStart/Delta

1.5

P0

3.3

Hangfire Jobs

异步 HTML 转 PDF + 上传，异常上报 Seq

1.5

P0

3.4

图表插件

C# 生成 Plotly JSON

1.0

P1

M3

里程碑

支持分屏预览，文档生成不阻塞

-

-

Phase 4: 原生脚本与 Code RAG (Week 4)

目标: Roslyn 引擎，代码理解。

ID

任务名称

描述

估时 (天)

优先级

4.1

ScriptRunner

Roslyn 引擎集成 + Console 重定向

2.0

P0

4.2

Live Console

实时日志推流逻辑

1.0

P0

4.3

Code Splitter

Roslyn 语法树解析器 (按方法切分)

2.0

P0

M4

里程碑

C# 脚本安全运行，RAG 理解代码结构

-

-

Phase 5: 交付与压测 (Week 5)
全链路测试，文档化