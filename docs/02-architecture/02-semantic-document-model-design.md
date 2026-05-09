# DevNexus AI 语义文档模型设计

## 1. 文档目的

本文档定义语义派生层使用的文档模型边界，回答以下问题：

- 什么是 SmartDocument
- SmartDocument 和 FileAsset / FileVersion 的关系
- 哪些能力属于语义层，哪些不属于

![核心数据实体关系](../assets/Domain_Entity_Relationship.png)

## 2. SmartDocument 的职责

SmartDocument 是语义派生物，不是原始文件事实源。

它负责承载：

- 文件内容的可读文本表示
- 文档摘要、标题层级、结构化分块
- 检索、引用、RAG、上下文压缩所需的语义信息
- 解析状态、质量信息与部分轻量派生元数据

它不再负责承载：

- 原始二进制文件事实
- 文件版本历史
- 真实文件输出结果
- 模板转换、批处理、格式保留等执行语义
- 任务工作区与执行器调度

### 2.2 原始文件事实源

原始文件相关事实应由统一文件资产平台负责：

- UploadSession
- FileAsset
- FileVersion
- FileTask

因此，任何需要“真实处理文件”的场景，都应从 FileAsset / FileVersion 出发，而不是从 SmartDocument 出发。

## 3. 语义模型结构

## 3.1 核心对象

### SmartDocument

当前模型围绕以下维度组织：

- TraceId: 解析链路追踪标识
- SourceAssetId: 来源文件资产 ID
- SourceVersionId: 来源文件版本 ID
- Status: 解析状态
- Content: 可供模型阅读的文本主体
- Chunks: 分块结果
- Summary: 文档摘要
- Metadata: 轻量派生元数据
- ParsedAt: 完成时间

### Metadata 内容

Metadata 适合保存：

- MIME 类型、扩展名、页数、sheet 名称、语言等轻量属性
- 提取策略、耗时、降级信息、失败原因
- OCR 是否启用、是否截断、是否只抽样等说明性标记

Metadata 不应继续保存：

- 输出文件路径
- 模板映射摘要
- 任务执行阶段产物
- 仅用于文件运行时的控制信息

## 3.2 分块模型

Chunks 应服务于检索和上下文构建，而不是服务于文件运行时。

每个 Chunk 建议包含：

- ChunkId
- Text
- TokenCount
- ChunkType
- SourceLocation
- EmbeddingStatus

## 4. 文件类型在语义层的处理原则

### 文本文档

- 提取正文
- 保留标题层级
- 生成摘要
- 供聊天引用与检索使用

### 表格文档

- 提取表头、样例行、统计摘要
- 保留可引用的表格文本表示
- 不在语义层承担模板填充或格式保留

### 图片与 PDF

- 提取文本、页面摘要、图片描述
- 保留必要的 OCR / Vision 元数据
- 不把 Vision 结果误当作文件运行时结果

### 代码文件

- 提取代码文本、语言、基础结构信息
- 服务于解释、检索、引用、审查
- 不等同于实际构建、执行、改写结果

## 5. 与文件平台的关系

语义层与文件平台应通过显式关联协作：

1. 用户上传原始文件，平台生成 FileAsset / FileVersion。
2. 派生管线读取原始文件，生成 SmartDocument。
3. 聊天、检索、RAG 使用 SmartDocument。
4. 文件任务、外部执行器、结果回灌使用 FileAsset / FileVersion / FileTask。
5. 若执行结果需要再次被理解，再从输出资产生成新的 SmartDocument。

## 6. 当前边界

当前模型边界：

- SmartDocument 不承载真实文件转换能力。
- 文件任务运行时使用通用外部执行入口。
- Excel、PDF、图片等格式处理由文件任务 Runner 或语义解析器按职责承担。

SmartDocument 是语义事实源，不是文件处理主入口。

## 7. 设计收益

这样的拆分可以带来三个直接收益：

- 语义层职责单一，便于优化抽取、摘要、检索质量。
- 文件运行时可以自由接入 Python、CLI 或其他执行器，而不污染 SmartDocument 模型。
- 前端可以同时展示“可读语义状态”和“可执行文件状态”，避免继续混淆。
