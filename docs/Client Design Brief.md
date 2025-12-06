前端设计简略文档 (Client Design Brief)

项目名称

DevNexus AI Client

技术栈

.NET MAUI + Blazor Hybrid

目标平台

Windows (Desktop), macOS, iOS, Android

版本

v1.0

1. 设计哲学 (Design Philosophy)

Immersive (沉浸式): 无边框窗口，使用 Mica/Acrylic (亚克力) 材质作为背景，让应用与操作系统深度融合。

Reactive (响应式): 基于 SignalR 事件驱动 UI 更新，拒绝手动刷新。

Code-First UX: 专为开发者设计，强调代码阅读体验 (Monaco) 和终端体验 (Console)。

2. 核心架构

Hybrid 模式: 使用 BlazorWebView 承载 UI。

C# (MAUI): 处理系统级能力（窗口管理、文件下载、托盘图标、本地通知）。

HTML/CSS (Blazor): 处理复杂的 UI 渲染（聊天流、图表、编辑器）。

代码复用: 直接引用服务端提供的 DevNexus.Shared NuGet 包，DTO 完全一致。

3. 关键 UI 组件设计

3.1 主窗口 (The Shell)

Windows: 隐藏原生 TitleBar，使用 HTML/CSS 自绘可拖拽的标题栏 (App Region Drag)，集成红绿灯控制按钮。

背景: 透明 WebView，透出 MAUI 层的 Acrylic 模糊效果。

3.2 聊天流 (The Stream)

流式打字机: 监听 TextDelta，使用 JS 实现平滑打字机动画，自动识别 Markdown 代码块并触发 Prism.js 高亮。

思维链面板: 收到 ThoughtChain 区块时，渲染一个灰色的、默认折叠的 <details> 面板，内部实时追加思考文本。

交互卡片:

SqlApprovalCard: 红色边框，带 "Approve/Reject" 按钮。

WebSearchGrid: 网格展示搜索来源 Favicon 和标题。

3.3 分屏资产预览 (Artifact Split View)

布局: CSS Grid。默认单栏，收到 ArtifactStart 信号后，平滑过渡到双栏布局 (50/50)。

Monaco Editor: 右侧嵌入 Monaco Editor (VS Code 内核)。

只读模式: 实时显示 AI 生成的代码。

Diff 模式: 当 AI 修改代码时，显示 Inline Diff 视图。

HTML 预览: 如果资产是 HTML，通过 <iframe> 沙箱渲染，支持缩放。

3.4 实时终端 (Live Console)

组件: 类似 XTerm.js 风格的黑色面板。

行为: 监听 ScriptRunner 区块的 Output 字段，实时追加绿色日志流，脚本执行完毕后显示 Exit Code。

3.5 图表渲染

组件: 封装 Plotly.js 为 Blazor 组件。

数据: 直接绑定服务端下发的 ChartPayload.DataJson，支持缩放和导出图片。

4. MAUI 原生能力集成

自动更新: 启动时调用服务端 Version API，如下载了新包，调用系统安装程序进行更新。

任务栏进度: 长任务执行时，设置 Windows 任务栏图标的进度条状态 (Indeterminate/Value)。

本地存储: 使用 SQLite 缓存聊天记录，支持离线查看历史。