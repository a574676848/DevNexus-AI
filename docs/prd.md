# DevNexus AI：智能研发工作站 PRD v4.0

**Subtitle:** A Generative UI Operating System for Developers

---

## 📋 文档元信息

| 属性 | 内容 |
|---|---|
| **项目代号** | DevNexus (Development Nexus - 研发枢纽) |
| **版本** | v4.0 - Rich Interactive Experience Edition |
| **技术栈** | .NET 10 + Aspire + Semantic Kernel + Fluent UI Blazor |
| **核心范式** | Generative UI + Chat as OS + Executable Artifacts |
| **文档焦点** | 富交互体验设计与实现规范 |

---

## 🎯 产品愿景 (Vision Statement)

**我们不是在构建聊天机器人，我们在构建下一代操作系统的雏形。**

DevNexus AI 重新定义了开发者与 AI 的交互方式：

- **对话即命令行** - 自然语言是最强大的编程接口
- **工件即应用** - 每个 AI 响应都是可执行的微型应用
- **界面即数据** - UI 组件本身承载完整的上下文与状态

---

## 🎨 设计哲学 (Design Philosophy)

### 1. Native & Fluent：原生体验至上

#### 视觉语言规范

**材质系统 (Material System)**

```
┌─────────────────────────────────────┐
│ 主窗口背景: Mica (云母材质)           │
│ - 半透明，与桌面壁纸融合              │
│ - 动态响应系统主题变化                │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ 侧边栏/浮层: Acrylic (亚克力模糊)     │
│ - Blur Radius: 30px                 │
│ - Tint Opacity: 0.7                 │
│ - Noise Texture: 2%                 │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ 卡片组件: Layered Shadows            │
│ - Elevation 4: 0 2px 8px rgba(0,0,0,0.12) │
│ - Elevation 8: 0 4px 16px rgba(0,0,0,0.16) │
└─────────────────────────────────────┘
```

**圆角规范 (Border Radius Tokens)**

| 组件类型 | 圆角值 | 使用场景 |
|---------|-------|---------|
| Small | 4px | 按钮、标签 |
| Medium | 8px | 输入框、小卡片 |
| Large | 12px | 对话气泡、工件容器 |
| XLarge | 16px | 模态窗口、主容器 |

**光照效果 (Reveal Highlight)**

- **Hover State**: 光标周围产生柔和的白色光晕（Radial Gradient）
- **Press State**: 按钮产生 2px 的下沉效果 + 阴影收缩
- **Focus State**: 2px 蓝色描边 + 外发光效果

#### 动效设计原则


**动画时长规范**

| 动作类型 | 时长 | 适用场景 |
|---------|------|---------|
| Micro | 100ms | 按钮反馈、开关切换 |
| Short | 200ms | 卡片展开、工具提示 |
| Medium | 400ms | 页面切换、工件渲染 |
| Long | 600ms | 登录过渡、大型数据加载 |

---

### 2. Chat as OS：对话即操作系统,聊天窗口不仅是对话框，而是命令行（Terminal）与画布（Canvas）的结合体

**上下文感知**

- **文件拖入检测**: 自动提取文件类型、语言、行数
- **剪贴板智能识别**: 
  - 代码 → 自动格式化并高亮
  - JSON → 自动展开为树形视图
  - 图片 → 触发 OCR 或视觉理解

---

### 3. Artifacts over Text：工件重于文字,AI 的输出不仅仅是文字，而是“工件 (Artifacts)”——即可交互、可执行、可编辑的 Blazor 组件


**工件核心特性**

1. **可交互性 (Interactive)**
   - 所有按钮、开关、输入框都是真实可操作的
   - 操作结果立即反馈，无需刷新

2. **可执行性 (Executable)**
   - SQL 工件可直连数据库执行
   - 代码工件可直接写入本地文件系统
   - API 工件可发起真实 HTTP 请求

3. **可编辑性 (Editable)**
   - 用户可修改工件内容（如编辑 SQL）
   - 修改后点击 "Regenerate" 可让 AI 继续优化

4. **状态持久化 (Stateful)**
   - 工件状态自动保存到聊天历史
   - 切换对话后再回来，工件状态完整恢复

---

## 🏗️ 系统架构 (System Architecture)

### 物理部署拓扑

```
┌─────────────────────────────────────────────────────┐
│         💻 Local Development Machine                │
│              (Windows 11 Host)                      │
│                                                     │
│  ┌──────────────────────────────────────────────┐ │
│  │  🎨 Blazor WebAssembly (AOT Compiled)        │ │
│  │     - Fluent UI Components                   │ │
│  │     - Widget Rendering Engine                │ │
│  │     - Local File System Bridge               │ │
│  └──────────────────────────────────────────────┘ │
│               ↕ (SignalR / HTTP)                    │
│  ┌──────────────────────────────────────────────┐ │
│  │  ⚙️ .NET Aspire AppHost                      │ │
│  │     - Service Orchestration                  │ │
│  │     - Health Monitoring                      │ │
│  └──────────────────────────────────────────────┘ │
│               ↕                                     │
│  ┌──────────────────────────────────────────────┐ │
│  │  🧠 API Service (.NET 10)                    │ │
│  │     - Semantic Kernel Runtime               │ │
│  │     - Plugin Manager                         │ │
│  │     - Hybrid Stream Protocol                 │ │
│  └──────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                        ↕
              (TCP/IP: 192.168.10.99)
                        ↕
┌─────────────────────────────────────────────────────┐
│        🏢 Remote Data Center Server                 │
│                                                     │
│  🐘 PostgreSQL:5432    🔍 Qdrant:6333              │
│  📊 Seq:5341           🚀 Redis:6379                │
└─────────────────────────────────────────────────────┘
```

### 数据流：混合流协议 (Hybrid Stream Protocol)

```json
// 帧类型枚举
type StreamFrameType = 
  | "Thinking"   // AI 思考过程 (用于展示 "正在搜索文档...")
  | "Content"    // 普通文本片段 (用于打字机效果)
  | "Artifact"   // 复杂组件 (用于 Widget Factory)
  | "Reference"  // 引用来源 (RAG 检索到的文档)
  | "Error"      // 错误信息
  | "Done";      // 流结束

// 帧结构示例
{
  "type": "Content",
  "payload": "你好，"
}
{
  "type": "Content",
  "payload": "DevNexus。"
}
{
  "type": "Artifact",
  "payload": {
    "id": "sql-1024",
    "component": "SqlRunnerWidget",
    "props": { "sql": "SELECT * FROM Users", "readOnly": false }
  }
}
```

**前端 Widget Factory 解析流程**

在 Blazor 中，我们不再使用简单的 `@Message.Content` 来显示消息。我们需要构建一个**混合渲染器**。

在 DevNexus AI 中，Widget Factory 是核心调度器：

对于 普通对话，它是一个高级 Markdown 浏览器，支持打字机、引用溯源和代码高亮。

对于 特定任务，它是一个动态 UI 容器，能够将 SQL 编辑器、Jira 卡片、Diff 视图无缝嵌入到对话流中。

### 渲染逻辑 (伪代码)

工厂会维护一个 `List<RenderFragment>`。当数据流进来时，它像“贪吃蛇”一样吞噬数据：

1.  **文本帧**: 累积到一个 Buffer 中，实时刷新 Markdown 渲染器，产生“打字机”视觉效果。
2.  **工件帧**: 立即截断当前的文本 Buffer，在下方插入一个 `DynamicComponent`，并将 JSON props 注入组件参数。

### Blazor 实现核心 (`WidgetFactory.razor`)

```razor
@foreach (var item in _renderItems)
{
    @if (item.Type == "Text")
    {
        <!-- 普通对话：Markdown 渲染 -->
        <div class="markdown-body animate-fade-in">
            <MarkdownRenderer Content="@item.TextBuffer" />
        </div>
    }
    else if (item.Type == "Artifact")
    {
        <!-- 工件：动态组件加载 -->
        <div class="artifact-container my-3 scale-in-center">
            <DynamicComponent Type="@GetWidgetType(item.ComponentName)" 
                              Parameters="@item.Parameters" />
        </div>
    }
}
```
---

## 🎬 核心功能模块：富交互设计详解

### Module 1: 沉浸式登录体验 (Immersive Login)
视觉风格: Windows 11 锁屏风格。背景使用 Bing每日壁纸的高斯模糊 (Acrylic Blur)。

组件: FluentCard 居中悬浮。

交互:

输入框拥有“呼吸灯”聚焦效果。

登录按钮点击后，卡片通过 CSS 动画“缩小并飞入”屏幕中央，无缝过渡到主界面。

支持通过 Google 账号 SSO (OpenID Connect) 一键登入。

---

### Module 2: 控制枢纽 (Control Hub)

**设计理念**: 配置面板不应该是枯燥的表单，而应该是**实时的、可验证的仪表盘**。

#### Section A: AI 供应商配置

**交互式配置卡片**

```
┌──────────────────────────────────────────────────┐
│ 🤖 AI Provider Configuration                     │
├──────────────────────────────────────────────────┤
│                                                  │
│  ○ OpenAI    ○ Azure OpenAI    ○ DeepSeek       │
│  ● Ollama (本地)                                 │
│                                                  │
│  ┌─────────────────────────────────────────┐   │
│  │ Endpoint: http://localhost:11434        │   │
│  │ Model:    [llama3.2:latest ▼]          │   │
│  │                                         │   │
│  │ [Test Connection]  Status: 🟡 Testing   │   │
│  └─────────────────────────────────────────┘   │
│                                                  │
│  ✅ Connected in 87ms                            │
│  └─ Available Models: llama3.2, codellama       │
│                                                  │
└──────────────────────────────────────────────────┘
```

**状态指示灯动画**

| 状态 | 颜色 | 动画效果 |
|-----|------|---------|
| **Idle** | 灰色 🔘 | 静止 |
| **Testing** | 黄色 🟡 | 脉冲呼吸 (Pulsing, 1.5s 周期) |
| **Connected** | 绿色 🟢 | 从黄色渐变到绿色 (300ms) |
| **Error** | 红色 🔴 | 震动 + 错误消息弹出 |

**测试连接的实时反馈**


#### Section B: 数据源配置

**PostgreSQL 配置卡片**

```
┌──────────────────────────────────────────────────┐
│ 🐘 PostgreSQL Database                           │
├──────────────────────────────────────────────────┤
│  Host: 192.168.10.99                             │
│  Port: 5432                                      │
│  Database: devnexus                              │
│  User: postgres                                  │
│                                                  │
│  Schema Version: v1.2.3                          │
│  ⚠️ Migration Required: v1.2.3 → v1.3.0          │
│                                                  │
│  [One-Click Migrate →]                           │
│                                                  │
└──────────────────────────────────────────────────┘
```

**一键迁移交互流程**

```
User clicks [One-Click Migrate]
    ↓
Button → Loading State (Spinner)
    ↓
Progress Bar appears below:
┌─────────────────────────────────────┐
│ █████████████░░░░░░░░░░░░ 60%      │
│ Running migration 003_add_vector... │
└─────────────────────────────────────┘
    ↓
Success Animation:
    - Progress Bar fills to 100%
    - Button turns green with ✓
    - Card briefly flashes green border
    - Schema Version updates automatically
```

#### Section C: 本地代码库索引

**文件夹选择器 + 实时索引状态**

```
┌──────────────────────────────────────────────────┐
│ 📁 Local Codebase Indexing                       │
├──────────────────────────────────────────────────┤
│                                                  │
│  D:\Projects\MyERP                  [Change...] │
│                                                  │
│  Status: 🟡 Indexing (2,347 / 4,521 files)      │
│                                                  │
│  ┌────────────────────────────────────────┐    │
│  │ ████████████████░░░░░░░░░░ 52%        │    │
│  │ Processing: Controllers\UserApi.cs     │    │
│  └────────────────────────────────────────┘    │
│                                                  │
│  ETA: ~3 minutes                                │
│  Vector Store: Qdrant @ 192.168.10.99:6333     │
│                                                  │
└──────────────────────────────────────────────────┘
```

**索引完成动画**

```
Progress Bar → 100%
    ↓
Status 从 🟡 → 🟢
    ↓
卡片背景闪烁绿色光晕 (500ms)
    ↓
显示统计信息:
┌──────────────────────────────────────┐
│ ✅ Indexing Complete!                │
│ • 4,521 files processed              │
│ • 342,891 code chunks embedded       │
│ • 12.3 GB indexed                    │
│ • RAG is now ready                   │
└──────────────────────────────────────┘
```

---

### 智能对话与工件引擎 (The Artifact Engine)

这是 DevNexus 的灵魂所在。本节详细定义每种工件的**视觉规范、交互逻辑、状态管理**。
**基础布局**
Input Area: 不仅仅是文本框，支持 / 命令唤起插件菜单 (类似 Notion)。

Message Bubble: 用户气泡是半透明磨砂玻璃效果；AI 气泡是完全透明背景，强调内容本身。

#### “普通对话”的富交互设计 (Normal Conversation UX)

在 DevNexus 中，即使是“普通对话”（例如：问答、解释概念、闲聊），也必须遵循 **Native & Fluent** 的高标准。

#### A. 思考状态的可视化 (The Thinking Process)

当 AI 正在检索 Qdrant 或生成逻辑时，不要只显示一个 Loading 圈。

  * **交互**: 气泡下方出现一行微小的灰色文字，伴随脉冲动画。
  * **内容**: 实时显示 Semantic Kernel 的步骤。
      * *Step 1*: "Parsing user intent..."
      * *Step 2*: "Searching vector database for 'LoginController'..."
      * *Step 3*: "Synthesizing answer..."
  * **价值**: 让用户感知到 AI 是在“工作”而不是“卡死”，增加信任感。

#### B. 增强型 Markdown (Enhanced Markdown)

普通文本通过 Markdown 渲染，但我们对其进行了**原生化改造**：

1.  **行内代码 (Inline Code)**:
      * 不仅仅是灰色背景。它看起来像一个小型的 Terminal 片段，字体使用 `Cascadia Code`。
      * **交互**: 鼠标悬停时，背景色微亮；点击可直接复制。
2.  **代码块 (Code Blocks)**:
      * **UI**: 使用 **Monaco Editor (Read-only)** 的配色方案，带有 Windows 11 风格的圆角和阴影。
      * **Header**: 包含语言图标 (C\# Icon) + 文件名探测 (e.g., "Program.cs") + "Copy" 按钮。
      * **交互**: 长代码块自动折叠，显示 "Show more"，避免刷屏。
3.  **引用/脚注 (Citations)**:
      * 当 RAG 检索生效时，文末会出现 `[1]` `[2]`。
      * **交互**: 这是一个 `FluentTooltip`。鼠标悬停时，弹出一个小卡片，显示引用的原始文档片段（来源于 Qdrant），甚至提供“打开源文件”的链接。

#### C. 流式打字机效果 (Fluent Typing)

  * **光标**: 在文本生成的末尾，始终跟随着一个闪烁的 `|` 光标（Fluent 蓝），模拟真实输入感。
  * **平滑滚动**: 随着文本生成，聊天窗口自动平滑滚动到底部（使用 JS Interop 控制），但如果用户手动向上滚动查看历史，自动滚动由于“用户介入”而暂停，避免干扰阅读。

#### D. 快捷操作栏 (Action Chips)

在普通对话结束后，AI 会根据内容在气泡下方生成一组 **Action Chips**（小药丸按钮）：

  * 如果 AI 解释了一个概念 -\> 推荐: `Explain deeper`, `Give an example`.
  * 如果 AI 写了一段代码 -\> 推荐: `Refactor`, `Add comments`.
  * **交互**: 点击 Chip 直接将预设指令发送给 AI，无需打字。

#### 场景对比：普通交互 vs. 工件交互

##### 场景一：普通对话 (Q: "什么是 .NET Aspire？")

1.  **Backend**: SK 查询知识库 -\> LLM 生成文本。
2.  **Stream**: 连续发送 `Content` 帧。
3.  **Frontend**:
      * 显示 "Searching knowledge base..." (Thinking Frame)。
      * Markdown 文本逐字出现。
      * 文末出现 `[Microsoft Docs]` 引用标记。
      * 生成结束，光标消失。
      * 出现 Chips: `How to install?`, `Compare to K8s`.

##### 场景二：工件交互 (Q: "帮我查一下 prod 库的用户表")

1.  **Backend**: SK 识别意图 -\> 生成 SQL -\> 决定不发文本，发工件。
2.  **Stream**:
      * `Content` 帧: "好的，这是为您生成的查询语句："
      * `Artifact` 帧: `{ component: "SqlRunnerWidget", props: { sql: "..." } }`
3.  **Frontend**:
      * 先显示一行文字：“好的，这是为您生成的查询语句：”
      * **立即**在文字下方渲染一个 **SQL Editor 卡片**（带有语法高亮、运行按钮）。
      * **交互**: 用户不再看文字，而是直接去点击卡片上的 "Run" 按钮。

---