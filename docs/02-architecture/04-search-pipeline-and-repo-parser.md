# DevNexus-AI 搜索与代码仓库解析能力（代码实况）

## 1. 文档说明

本文档描述当前代码中已实现的搜索与仓库解析能力，不包含未落地规划。

权威事实源：`src/backend/DevNexus.Core/Services/Chat/ToolBlockExecutionCoordinator.cs` 等。

## 2. 智能网络搜索流水线

### 2.1 核心流程（已实现）

1. **URL 直接读取** → 使用 `<webpage>` 工具
2. **Git 仓库拦截** → 在代码层拦截 GitHub/GitLab 链接，重定向至 repo-parser 技能
3. **搜索查询** → 使用 `<search_web>` 工具
4. **深度内容分析** → 使用 `<advanced_search>` 工具

### 2.2 降级策略

- **阅读层** - JinaReader（优先）→ Firecrawl（兜底）

## 3. 代码仓库解析

### 3.1 当前实现

- 公开仓库解析路径已实现
- 执行目录位于用户受控工作区（`project/{userId}/workspaces/{workspaceId}`）
- 具体路径由 `UserWorkspaceService` 决定

### 3.2 语言识别

- 支持常见代码文件类型识别
- 具体覆盖范围以当前解析实现为准

### 3.3 技术栈推断

- 可基于仓库关键文件做基础技术栈推断
- 输出能力取决于当前实现与配置

### 3.4 执行隔离

- 建议在受控工作区内执行解析流程，避免越界访问

## 4. 风险防范

### 4.1 Token 保护

- 单次网页读取存在字符限制，具体数值以当前实现为准

### 4.2 反爬检测

- 当前实现包含基本的状态码与内容识别逻辑

### 4.3 Git URL 拦截

- 在 `ToolBlockExecutionCoordinator` 中实现仓库链接拦截

## 5. 配置说明

- **SearXNG**：复用 BaseUrl，建议本地容器部署
- **Tavily / Jina / Firecrawl**：在供应商管理面板配置 ApiKey 即可启用
