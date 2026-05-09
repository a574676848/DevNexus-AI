# DevNexus AI 图标库

这个文件夹包含了所有可复用的 SVG 图标组件。

## 使用方法

```razor
@using DevNexus.Client.Components.Icons

<ChatIcon Size="24" />
<AnalyticsIcon Size="20" Stroke="red" />
<SettingsIcon Size="22" StrokeWidth="2.5" />
```

## 可用图标

### 导航图标
- **ChatIcon** - 聊天/对话图标
- **AnalyticsIcon** - 数据分析图标
- **ControlHubIcon** - 控制中心/控制台图标
- **UserManagementIcon** - 用户管理图标
- **SettingsIcon** - 设置图标
- **MenuIcon** - 菜单图标

### 图标参数

所有图标都继承自 `IconBase` 组件，支持以下参数：

- `Size` (int, 默认: 24) - 图标尺寸
- `Fill` (string, 默认: "none") - 填充颜色
- `Stroke` (string, 默认: "currentColor") - 描边颜色
- `StrokeWidth` (string, 默认: "2") - 描边宽度
- `CssClass` (string?) - CSS 类名
- `Style` (string?) - 内联样式

## 添加新图标

1. 在此文件夹创建新的 `.razor` 文件
2. 继承 `IconBase` 组件
3. 使用命名空间 `@namespace DevNexus.Client.Components.Icons`
4. 定义 SVG 路径
5. 更新此 README 文件

### 示例模板

```razor
@namespace DevNexus.Client.Components.Icons
@inherits IconBase

<svg width="@Size" height="@Size" viewBox="0 0 24 24" fill="@Fill" stroke="@Stroke" 
     stroke-width="@StrokeWidth" stroke-linecap="round" stroke-linejoin="round" 
     class="@CssClass" style="@Style">
    <!-- SVG 路径 -->
    <path d="..."></path>
</svg>
```

## 设计规范

- 所有图标使用 24x24 的 viewBox
- 使用 `currentColor` 继承父元素的颜色
- 默认描边宽度为 2px
- 圆角使用 `round` 样式
- 保持简洁的视觉风格
