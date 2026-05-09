# 代码规范

> 本规范适用于当前仓库的后端、客户端、共享契约与文档改动。

本文档定义了 DevNexus AI 项目的代码风格和最佳实践。所有贡献者必须遵守这些规范，以确保代码质量和一致性。

---

## 目录

- [基础规范](#基础规范)
- [设计原则](#设计原则)
- [命名约定](#命名约定)
- [代码组织](#代码组织)
- [注释规范](#注释规范)
- [数据库规范](#数据库规范)
- [API 规范](#api-规范)
- [日志规范](#日志规范)
- [性能标准](#性能标准)
- [测试规范](#测试规范)

---

## 基础规范

### 1.1 文件编码与格式

| 规则 | 要求 | 说明 |
|------|------|------|
| **编码** | UTF-8 (无 BOM) | 所有文件统一使用 UTF-8 编码 |
| **缩进** | 4 个空格 | 禁用 Tab，使用 4 个空格缩进 |
| **行尾** | LF (`\n`) | Linux 风格换行符 |
| **尾随空格** | 不允许 | 删除行尾的空格 |
| **文件结尾** | 保留空行 | 每个文件末尾保留一个空行 |
| **单行长度** | ≤ 120 字符 | 超过则换行 |

**EditorConfig 配置：**

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_style = space
indent_size = 4
max_line_length = 120
```

### 1.2 文件组织

| 规则 | 要求 |
|------|------|
| **每文件一个主类** | 一个 `.cs` 文件只包含一个主要的类（辅助类除外） |
| **文件大小限制** | 单文件最大 500 行，超过则拆分或使用 `partial` |
| **命名空间** | 相关类放在同一命名空间，遵循目录结构 |

---

## 设计原则

### 2.1 SOLID 原则

DevNexus AI 严格遵守 SOLID 原则：

#### S - 单一职责原则 (Single Responsibility Principle)

一个类应该只有一个引起变化的原因。

**❌ 不好的例子：**

```csharp
public class UserService
{
    public void CreateUser(User user) { }
    public void SendEmail(string email, string message) { } // 违反 SRP
    public void LogActivity(string message) { } // 违反 SRP
}
```

**✅ 好的例子：**

```csharp
public class UserService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;

    public UserService(IEmailService emailService, ILogger<UserService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public void CreateUser(User user)
    {
        // 创建用户逻辑
        _emailService.SendWelcomeEmail(user.Email);
        _logger.LogInformation("用户 {UserId} 创建成功", user.Id);
    }
}
```

#### O - 开闭原则 (Open/Closed Principle)

对扩展开放，对修改关闭。

**✅ 好的例子：**

```csharp
public interface ILlmProvider
{
    Task<string> GenerateAsync(string prompt);
}

public class OpenAIProvider : ILlmProvider { }
public class GeminiProvider : ILlmProvider { }
```

#### L - 里氏替换原则 (Liskov Substitution Principle)

子类应该能够替换父类。

#### I - 接口隔离原则 (Interface Segregation Principle)

客户端不应该依赖它不需要的接口。

**❌ 不好的例子：**

```csharp
public interface IRepository<T>
{
    Task<T> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<int> CountAsync(); // 不是所有仓储都需要
    Task<IEnumerable<T>> SearchAsync(string query); // 不是所有仓储都需要
}
```

**✅ 好的例子：**

```csharp
public interface IReadRepository<T>
{
    Task<T> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
}

public interface IWriteRepository<T>
{
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

public interface ISearchableRepository<T>
{
    Task<IEnumerable<T>> SearchAsync(string query);
}
```

#### D - 依赖倒置原则 (Dependency Inversion Principle)

高层模块不应该依赖低层模块，两者都应该依赖抽象。

**✅ 好的例子：**

```csharp
public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
}

public class ChatController : ControllerBase
{
    private readonly IChatService _chatService; // 依赖抽象

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }
}
```

### 2.2 代码度量

| 度量 | 限制 | 说明 |
|------|------|------|
| **方法行数** | < 50 行 | 超过则拆分为多个方法 |
| **方法参数** | < 5 个 | 超过考虑使用参数对象 |
| **类公共方法** | < 15 个 | 超过考虑拆分类 |
| **嵌套深度** | < 4 层 | 避免深度嵌套 |
| **圈复杂度** | < 10 | 保持逻辑简单 |

**示例：**

```csharp
// ❌ 嵌套过深
public void ProcessData(List<Item> items)
{
    foreach (var item in items)
    {
        if (item.IsValid)
        {
            if (item.Status == Status.Active)
            {
                if (item.HasPermission)
                {
                    // 嵌套过深
                }
            }
        }
    }
}

// ✅ 提前返回
public void ProcessData(List<Item> items)
{
    foreach (var item in items)
    {
        if (!item.IsValid) continue;
        if (item.Status != Status.Active) continue;
        if (!item.HasPermission) continue;
        
        // 处理逻辑
    }
}
```

### 2.3 DRY 原则 (Don't Repeat Yourself)

避免代码重复，提取共同逻辑为方法或类。

**✅ 好的例子：**

```csharp
// 提取公共逻辑
private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
        }
    }
    throw new InvalidOperationException("重试次数超过限制");
}
```

### 2.4 依赖注入

| 规则 | 要求 |
|------|------|
| **注入方式** | 仅使用构造函数注入 |
| **Service Locator** | 禁止使用 |
| **生命周期** | 正确选择 Singleton/Scoped/Transient |
| **循环依赖** | 避免循环依赖 |
| **作用域冲突** | 禁止将 Scoped 注入 Singleton |

**✅ 好的例子：**

```csharp
public class ChatService : IChatService
{
    private readonly ILlmProvider _llmProvider;
    private readonly IMemoryService _memoryService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ILlmProvider llmProvider,
        IMemoryService memoryService,
        ILogger<ChatService> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

---

## 命名约定

### 3.1 命名风格

| 类型 | 风格 | 示例 |
|------|------|------|
| **类** | PascalCase | `UserService`, `ChatController` |
| **接口** | I + PascalCase | `IUserService`, `IChatService` |
| **方法** | PascalCase | `GetUserById`, `SendMessage` |
| **属性** | PascalCase | `UserId`, `UserName` |
| **字段** (private) | _camelCase | `_userService`, `_logger` |
| **参数** | camelCase | `userId`, `userName` |
| **局部变量** | camelCase | `user`, `message` |
| **常量** | PascalCase | `MaxRetryCount`, `DefaultTimeout` |
| **枚举** | PascalCase | `UserStatus`, `MessageType` |
| **枚举值** | PascalCase | `Active`, `Inactive` |

### 3.2 命名指南

**✅ 好的命名：**

```csharp
public class UserService { }
public interface IUserRepository { }
public async Task<User> GetUserByIdAsync(int userId) { }
```

**❌ 不好的命名：**

```csharp
public class usrSvc { } // 缩写不清晰
public interface UserRepository { } // 接口缺少 I 前缀
public async Task<User> GetUsr(int id) { } // 缺少 Async 后缀
```

### 3.3 布尔变量命名

使用 `Is`, `Has`, `Can`, `Should` 等前缀：

```csharp
public bool IsActive { get; set; }
public bool HasPermission { get; set; }
public bool CanEdit { get; set; }
public bool ShouldRetry { get; set; }
```

---

## 代码组织

### 4.1 类成员顺序

```csharp
public class Example
{
    // 1. 常量
    private const int MaxItems = 100;

    // 2. 静态字段
    private static readonly ILogger _staticLogger = LoggerFactory.Create();

    // 3. 私有字段
    private readonly IService _service;
    private string _cache;

    // 4. 构造函数
    public Example(IService service)
    {
        _service = service;
    }

    // 5. 公共属性
    public int Id { get; set; }
    public string Name { get; set; }

    // 6. 公共方法
    public void PublicMethod() { }

    // 7. 私有方法
    private void PrivateMethod() { }
}
```

### 4.2 Using 指令

```csharp
// 系统命名空间
using System;
using System.Collections.Generic;
using System.Linq;

// 第三方库
using Microsoft.Extensions.Logging;
using Semantic.Kernel;

// 项目命名空间
using DevNexus.Core.Abstractions;
using DevNexus.Domain.Entities;
```

---

## 注释规范

### 5.1 XML 文档注释

所有公共 API 必须有 XML 文档注释：

```csharp
/// <summary>
/// 用户服务，提供用户管理功能
/// </summary>
public class UserService : IUserService
{
    /// <summary>
    /// 根据用户 ID 获取用户信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户实体，如果不存在则返回 null</returns>
    /// <exception cref="ArgumentException">当 userId 小于等于 0 时抛出</exception>
    public async Task<User?> GetUserByIdAsync(int userId)
    {
        if (userId <= 0)
            throw new ArgumentException("用户 ID 必须大于 0", nameof(userId));

        return await _repository.GetByIdAsync(userId);
    }
}
```

### 5.2 行内注释

复杂逻辑必须添加注释（使用中文）：

```csharp
// ✅ 好的注释
// 使用指数退避策略重试，避免瞬时故障
for (int i = 0; i < maxRetries; i++)
{
    try
    {
        return await action();
    }
    catch (Exception ex) when (i < maxRetries - 1)
    {
        // 等待时间：2^i 秒
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
    }
}

// ❌ 不好的注释
// 循环重试
for (int i = 0; i < maxRetries; i++) { }
```

### 5.3 TODO 注释

```csharp
// TODO(@zhangsan, 2026-03-10): 实现缓存机制以提升性能
// FIXME(@lisi, 2026-03-15): 修复并发场景下的竞态条件
```

---

## 数据库规范

### 6.1 命名约定

| 元素 | 规范 | 示例 |
|------|------|------|
| **表名** | 复数 | `Users`, `Messages` |
| **主键** | `Id` | `Id` |
| **外键** | `{EntityName}Id` | `UserId`, `ChatSessionId` |
| **索引** | `IX_{Table}_{Column}` | `IX_Users_Email` |
| **外键约束** | `FK_{Table}_{RefTable}` | `FK_Messages_ChatSessions` |

### 6.2 必需字段

所有表必须包含：

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// 支持软删除的实体
public abstract class SoftDeleteEntity : BaseEntity
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

---

## API 规范

### 7.1 RESTful 规范

| 规则 | 示例 |
|------|------|
| **资源使用复数** | `/api/v1/users`, `/api/v1/messages` |
| **HTTP 动词** | GET, POST, PUT, DELETE, PATCH |
| **URL 版本化** | `/api/v1/...` |

### 7.2 响应格式

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
}

// 分页响应
public class PagedResponse<T> : ApiResponse<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

### 7.3 Swagger 注解

```csharp
/// <summary>
/// 获取用户列表
/// </summary>
/// <param name="page">页码（从 1 开始）</param>
/// <param name="pageSize">每页数量（1-100）</param>
/// <returns>用户列表</returns>
/// <response code="200">成功返回用户列表</response>
/// <response code="400">参数验证失败</response>
/// <response code="401">未授权</response>
[HttpGet]
[ProducesResponseType(typeof(PagedResponse<List<UserDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
{
    // 实现
}
```

---

## 日志规范

### 8.1 日志级别

| 级别 | 使用场景 |
|------|----------|
| **Trace** | 详细的调试信息 |
| **Debug** | 开发调试信息 |
| **Information** | 一般信息（默认） |
| **Warning** | 警告信息 |
| **Error** | 错误信息 |
| **Critical** | 严重错误 |

### 8.2 结构化日志

```csharp
// ✅ 好的日志
_logger.LogInformation(
    "用户 {UserId} 发起聊天，会话ID: {SessionId}, 消息长度: {MessageLength}",
    userId, sessionId, message.Length);

// ❌ 不好的日志
_logger.LogInformation($"用户 {userId} 发起聊天"); // 字符串插值不利于日志聚合
```

### 8.3 日志上下文

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = userId,
    ["TraceId"] = HttpContext.TraceIdentifier
}))
{
    _logger.LogInformation("处理用户请求");
}
```

---

## 性能标准

### 9.1 数据库性能

- ✅ 为高频查询字段添加索引
- ✅ 避免 `SELECT *`，只查询需要的字段
- ✅ WHERE 子句中避免使用函数
- ✅ 大数据集使用分页
- ✅ 批量操作使用事务
- ✅ 读密集场景使用缓存

### 9.2 API 性能

- ✅ 响应时间 < 500ms
- ✅ 启用响应压缩 (Gzip/Brotli)
- ✅ 使用 HTTP 缓存/ETag
- ✅ 异步处理长时间操作

### 9.3 前端性能

- ✅ 首屏加载 < 3s
- ✅ Bundle 大小 < 5MB
- ✅ 使用 AOT 编译
- ✅ 组件懒加载

---

## 测试规范

### 10.1 单元测试

```csharp
public class UserServiceTests
{
    [Fact]
    public async Task GetUserByIdAsync_ValidId_ReturnsUser()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var expectedUser = new User { Id = 1, Name = "Test User" };
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(expectedUser);
        var service = new UserService(mockRepo.Object);

        // Act
        var result = await service.GetUserByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test User", result.Name);
    }

    [Fact]
    public async Task GetUserByIdAsync_InvalidId_ThrowsException()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var service = new UserService(mockRepo.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetUserByIdAsync(0));
    }
}
```

### 10.2 测试覆盖率

> 覆盖率目标作为质量参考，当前仓库以构建验证与主流程冒烟测试为主。

- 目标：核心业务逻辑覆盖率 > 80%
- 目标：关键路径覆盖率 100%
- 包含边界条件测试

---

## 工具配置

### .editorconfig

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_style = space
indent_size = 4
csharp_new_line_before_open_brace = all
csharp_prefer_braces = true:warning
```

### StyleCop 规则

在项目中启用 StyleCop.Analyzers 以自动检查代码风格。

---

## 检查清单

在提交代码前，请确认：

- [ ] 代码遵循命名约定
- [ ] 公共 API 有 XML 文档注释
- [ ] 复杂逻辑有注释说明
- [ ] 没有硬编码的魔法值
- [ ] 使用依赖注入
- [ ] 添加了单元测试
- [ ] 所有测试通过
- [ ] 没有编译警告
- [ ] 使用 `dotnet format` 格式化代码

---

