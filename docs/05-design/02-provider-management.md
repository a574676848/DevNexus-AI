# DevNexus - 供应商管理指南

## 1. 当前定位

供应商管理的目标不是单纯“保存几个 API Key”，而是为当前平台的两条主线提供可切换的后端能力来源：

1. 聊天与语义能力。
2. 搜索、存储、嵌入和外部集成能力。

## 2. 当前支持的供应商类别

当前文档应以控制器和实际接口为准，主要包括：

- LLM Provider
- Embedding Provider
- Search Provider
- Storage Provider
- Model Pricing

对应接口位于：

- /api/v1/providers/llm
- /api/v1/providers/embedding
- /api/v1/providers/search
- /api/v1/providers/storage
- /api/v1/model-pricing

## 3. 当前设计原则

### 3.1 数据库存储

供应商配置由数据库持久化，而不是只依赖静态配置文件。

### 3.2 敏感信息加密

API Key 等敏感信息应通过平台加密配置保护，不直接明文散落在代码或普通配置里。

### 3.3 运行时切换

供应商切换不应要求重启整个系统，这对本地调试和多模型试验都很重要。

### 3.4 验证优先

新增或修改供应商后，应优先做连接验证，再把它投入真实链路。

## 4. 为什么供应商管理和文件平台有关

当前项目里，文件平台并不是孤立的。

它至少依赖两类供应商能力：

1. Storage Provider
   - 决定上传和结果文件存储在哪里。
2. LLM / Embedding / Search Provider
   - 决定语义解析、检索增强和部分智能体链路的可用性。

所以当你看到“文件上传成功但流程不通”时，也要考虑是不是存储或模型供应商配置有问题。

## 5. 当前最常用的接口组

### 5.1 LLM Provider

典型接口：

- GET /api/v1/providers/llm
- GET /api/v1/providers/llm/{id}
- GET /api/v1/providers/llm/default
- POST /api/v1/providers/llm
- PUT /api/v1/providers/llm/{id}
- DELETE /api/v1/providers/llm/{id}
- POST /api/v1/providers/llm/{id}/set-default
- POST /api/v1/providers/llm/{id}/validate

### 5.2 Storage Provider

典型接口：

- GET /api/v1/providers/storage
- GET /api/v1/providers/storage/{id}
- GET /api/v1/providers/storage/default
- POST /api/v1/providers/storage
- PUT /api/v1/providers/storage/{id}
- DELETE /api/v1/providers/storage/{id}
- POST /api/v1/providers/storage/{id}/validate

这组接口直接影响上传预签名、文件保存和结果回灌链路。

## 6. 使用建议

### 6.1 开发环境

- 至少配置一个可用 LLM Provider。
- 如果不接对象存储，确保本地存储模式可用。
- 修改供应商后及时做 validate。

### 6.2 生产环境

- 使用受控对象存储而不是依赖临时本地目录。
- 对主供应商和备用供应商做明确优先级设计。
- 使用环境变量或安全配置管理敏感密钥。

## 7. 常见误区

### 误区一：只配 LLM 就够了

不够。

如果要跑完整文件链路，还必须考虑存储供应商。

### 误区二：供应商配置只是设置页问题

不是。

它直接影响聊天、上传、语义解析、检索和文件平台闭环。

### 误区三：旧接口路径仍然可作为文档依据

不应该。

当前供应商文档应以 /api/v1/providers/* 这组正式路径为准。

## 8. 联调顺序建议

建议按这个顺序联调：

1. 配置并验证 LLM Provider。
2. 配置并验证 Storage Provider。
3. 验证登录和聊天。
4. 验证上传与 finalize。
5. 验证 FileTask 与结果回灌。

这样能最快定位问题到底出在模型、存储，还是业务链路本身。
