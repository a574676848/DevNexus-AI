# DevNexus-AI 搜索与代码仓库解析能力

## 1. 文档说明

本文档描述当前代码中已实现的搜索与仓库解析能力，不包含未落地规划。

权威事实源：`src/backend/DevNexus.Core/Services/Chat/ToolBlockExecutionCoordinator.cs` 等。

## 2. 智能网络搜索流水线

### 2.1 核心流程（已实现）

1. **URL 直接读取** → 使用 `<webpage>` 工具
2. **Git 仓库拦截** → 在代码层拦截 GitHub/GitLab 等仓库链接，重定向至 repo-parser 技能
3. **搜索查询** → 使用 `<search_web>` 工具
4. **深度内容分析** → 使用 `<advanced_search>` 工具

### 2.2 降级策略

- **阅读层** - JinaReader（优先）→ Firecrawl（兜底）

## 3. 代码仓库解析

### 3.1 当前实现

- 公开仓库解析路径已实现
- 执行目录使用真实本机路径，调用方应显式传入可写的 `workingDirectory` 或脚本 `--workdir`
- `userId` / `sessionId` 仅用于会话隔离、审批、审计与 checkpoint，不再作为本机路径访问边界
- 技能目录按本机路径解析，匹配到 repo-parser 技能后由 HostService / CLI 主链在指定工作目录中执行，不要求 `SKILL.md` 自带插件声明

### 3.2 语言识别

- 支持常见代码文件类型识别
- 具体覆盖范围以当前解析实现为准

### 3.3 技术栈推断

- 可基于仓库关键文件做基础技术栈推断
- 输出能力取决于当前实现与配置

### 3.4 执行边界

- 建议为解析流程传入专用本机工作目录，避免把仓库代码克隆到技能源码目录
- 路径是否可读写由操作系统和服务运行账户权限决定；命令审批由聊天工具栏的 Agent 审批模式和 CLI 策略共同裁决，checkpoint 仍由 CLI 主链记录
- 后台清理任务只处理平台托管的过期资源，不扫描或删除用户传入的真实 `workingDirectory`

## 4. 风险防范

### 4.1 Token 保护

- 单次网页读取存在字符限制，具体数值以当前实现为准

### 4.2 反爬检测

- 当前实现包含基本的状态码与内容识别逻辑
- `ToolBlockExecutionCoordinator` 和 `WebSearchPlugin.ReadWebpageAsync` 都会先拦截 Git 仓库 URL，并返回 `recommendedSkill=repo-parser`

### 4.3 Git URL 拦截

- 在 `ToolBlockExecutionCoordinator` 中实现仓库链接拦截

## 5. 配置说明

- **SearXNG**：复用 BaseUrl，建议本地容器部署
- **Tavily / Jina / Firecrawl**：在供应商管理面板配置 ApiKey 即可启用
