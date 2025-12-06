# DevNexus AI 后端行动计划

> **文档版本**: v1.1  
> **制定日期**: 2025年12月6日  
> **最后更新**: 2025年12月6日  
> **预计周期**: 3 周 (15 人天)  
> **当前进度**: Sprint 1-2 完成 (65%)

---

## 一、现状评估

### 已完成模块

| 模块 | 完成度 | 说明 |
|------|--------|------|
| Aspire 基础架构 | 100% | 解决方案结构完整，服务编排就绪 |
| 数据模型 (EF Core) | 100% | User/ChatSession/ChatMessage/Artifact，JSONB + 树状结构 |
| Shared 库 | 100% | BlockType (7种)、DTOs、Enums 完整定义 |
| Semantic Kernel 集成 | 90% | KernelService + LLMProviderFactory + 流式响应 |
| Block 解析器 | 85% | 思考链/代码块/Artifact 解析已实现 |
| SignalR ChatHub | **95%** | ✅ Redis 背板 + 多端广播 + 人机回环 |
| 基础设施 | 70% | PostgreSQL/Redis/Seq 已配置 |
| **认证系统** | **100%** | ✅ ASP.NET Identity + JWT + RefreshToken |
| **Token 审计** | **100%** | ✅ SK Filter + Seq 结构化日志 |
| **版本检查 API** | **100%** | ✅ SystemController 完成 |

### 待完成关键缺口

| 优先级 | 缺口 | 影响范围 | 状态 |
|--------|------|---------|------|
| ~~P0~~ | ~~Redis SignalR 背板~~ | ~~多实例部署、多端同步~~ | ✅ 已完成 |
| ~~P0~~ | ~~人机回环 (Approve/Reject)~~ | ~~敏感操作审批流程~~ | ✅ 已完成 |
| ~~P1~~ | ~~ASP.NET Identity~~ | ~~用户登录~~ | ✅ 已完成 (管理员种子) |
| ~~P1~~ | ~~RefreshToken 机制~~ | ~~移动端长期登录~~ | ✅ 已完成 |
| ~~P1~~ | ~~Token 审计 (SK Filter)~~ | ~~LLM 成本监控~~ | ✅ 已完成 |
| P2 | 上下文缓存优化 | 首字延迟优化 | 🔄 Sprint 3 |

---

## 二、行动计划

### Sprint 1: 通信层加固 (Week 1)

**目标**: 完善 SignalR 基础设施，支持生产级多实例部署

#### Task 1.1: 启用 Redis SignalR 背板
- **优先级**: P0
- **工时**: 0.5 天
- **文件**: 
  - `DevNexus.ApiService/Program.cs`
  - `DevNexus.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`
- **操作**:
  1. 添加 NuGet 包: `Microsoft.AspNetCore.SignalR.StackExchangeRedis`
  2. 配置 `AddStackExchangeRedis()` 到 SignalR 服务
  3. 验证多实例消息广播

```csharp
// 目标配置
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("DevNexus");
    });
```

#### Task 1.2: 实现多端广播逻辑
- **优先级**: P0
- **工时**: 1 天
- **文件**: 
  - `DevNexus.ApiService/Hubs/ChatHub.cs`
  - `DevNexus.Core/Services/ChatService.cs`
- **操作**:
  1. 用户连接时加入 User Group (`Groups.AddToGroupAsync`)
  2. 消息推送改为 Group 广播 (`Clients.Group(userId)`)
  3. 实现连接管理 (OnConnectedAsync/OnDisconnectedAsync)

#### Task 1.3: 人机回环接口
- **优先级**: P0
- **工时**: 1.5 天
- **文件**: 
  - `DevNexus.ApiService/Hubs/ChatHub.cs`
  - `DevNexus.Core/Abstractions/IChatService.cs`
  - `DevNexus.Core/Services/ChatService.cs`
  - `DevNexus.Shared/DTOs/ApprovalRequest.cs` (新建)
- **操作**:
  1. 定义 `ApprovalRequest` DTO (ActionId, ActionType, Payload)
  2. 实现 `ApproveAction(ApprovalRequest)` Hub 方法
  3. 实现 `RejectAction(ApprovalRequest)` Hub 方法
  4. 使用 `TaskCompletionSource` 管理审批等待状态

#### Task 1.4: Token 审计 Filter
- **优先级**: P1
- **工时**: 1 天
- **文件**: 
  - `DevNexus.Core/Services/LLM/TokenAuditFilter.cs` (新建)
  - `DevNexus.Core/Services/LLM/KernelService.cs`
- **操作**:
  1. 实现 `IFunctionInvocationFilter` 接口
  2. 记录 InputTokens, OutputTokens, ModelId, Duration
  3. 使用结构化日志写入 Seq
  4. 添加 TraceId 关联

```csharp
// 审计日志格式
_logger.LogInformation(
    "[AI.TokenAudit] Completion finished | Model={Model} InputTokens={Input} OutputTokens={Output} Duration={Duration}ms",
    modelId, inputTokens, outputTokens, duration);
```

**Sprint 1 里程碑**: SignalR 支持多实例，人机回环可用，Token 消耗可追踪

---

### Sprint 2: 身份认证体系 (Week 2)

**目标**: 建立完整的用户认证授权系统

#### Task 2.1: ASP.NET Identity 集成
- **优先级**: P1
- **工时**: 1.5 天
- **文件**: 
  - `DevNexus.Infrastructure/Models/ApplicationDbContext.cs`
  - `DevNexus.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`
  - `DevNexus.ApiService/Program.cs`
  - `DevNexus.Infrastructure/Models/User.cs` (修改)
- **操作**:
  1. 让 `User` 继承 `IdentityUser<Guid>`
  2. 配置 `AddIdentity<User, IdentityRole<Guid>>()`
  3. 创建 EF Core 迁移
  4. 移除硬编码测试代码

#### Task 2.2: 认证 API 端点
- **优先级**: P1
- **工时**: 1.5 天
- **文件**: 
  - `DevNexus.ApiService/Controllers/AuthController.cs` (新建)
  - `DevNexus.Core/Services/AuthService.cs` (新建)
  - `DevNexus.Shared/DTOs/Auth/` (新建目录)
- **操作**:
  1. 实现 `POST /api/auth/register`
  2. 实现 `POST /api/auth/login`
  3. 实现 `POST /api/auth/refresh-token`
  4. 实现 `POST /api/auth/logout`
  5. 定义 DTO: `RegisterRequest`, `LoginRequest`, `TokenResponse`

#### Task 2.3: RefreshToken 机制
- **优先级**: P1
- **工时**: 1 天
- **文件**: 
  - `DevNexus.Infrastructure/Models/RefreshToken.cs` (新建)
  - `DevNexus.Core/Services/AuthService.cs`
- **操作**:
  1. 创建 `RefreshToken` 实体 (Token, UserId, ExpiresAt, DeviceId)
  2. 实现 30 天滑动过期策略
  3. 支持设备指纹记录
  4. 实现 Token 撤销逻辑

#### Task 2.4: 客户端版本 API
- **优先级**: P2
- **工时**: 0.5 天
- **文件**: 
  - `DevNexus.ApiService/Controllers/SystemController.cs` (新建)
  - `DevNexus.Shared/DTOs/ClientVersionDto.cs` (新建)
- **操作**:
  1. 实现 `GET /api/system/client-version`
  2. 返回最新版本号、下载地址、是否强制更新
  3. 从配置文件读取版本信息

**Sprint 2 里程碑**: 用户可注册登录，支持长效 Token，客户端可检查更新

---

### Sprint 3: 性能优化与文档 (Week 3)

**目标**: 优化性能，完善 API 文档，准备 Phase 3

#### Task 3.1: 上下文缓存优化
- **优先级**: P2
- **工时**: 1 天
- **文件**: 
  - `DevNexus.Core/Services/ChatService.cs`
  - `DevNexus.Core/Services/ContextCacheService.cs` (新建)
- **操作**:
  1. 实现 `ContextCacheService` 封装 Redis 操作
  2. 缓存活跃会话最近 20 条消息
  3. 设置 30 分钟过期策略
  4. 消息发送时更新缓存

```csharp
// 缓存键格式
$"session:{sessionId}:context"
```

#### Task 3.2: 分布式锁服务
- **优先级**: P2
- **工时**: 0.5 天
- **文件**: 
  - `DevNexus.Core/Services/DistributedLockService.cs` (新建)
- **操作**:
  1. 基于 Redis SETNX 实现分布式锁
  2. 支持自动续期和超时释放
  3. 为后续 Wiki 生成、OSS 上传等任务准备

#### Task 3.3: 结构化日志增强
- **优先级**: P2
- **工时**: 0.5 天
- **文件**: 
  - `DevNexus.ApiService/Middlewares/RequestLoggingMiddleware.cs` (新建)
  - `DevNexus.ApiService/Program.cs`
- **操作**:
  1. 实现请求日志中间件
  2. 注入 TraceId, SpanId, UserId
  3. 记录请求耗时和状态码

#### Task 3.4: API 文档完善
- **优先级**: P3
- **工时**: 1 天
- **文件**: 
  - 所有 Controller 和 Hub
  - `DevNexus.ApiService/DevNexus.ApiService.csproj`
- **操作**:
  1. 为所有接口添加 XML 注释
  2. 启用 XML 文档生成
  3. 配置 Swagger 分组 (Auth/Chat/System)
  4. 添加请求/响应示例

#### Task 3.5: 健康检查端点
- **优先级**: P3
- **工时**: 0.5 天
- **文件**: 
  - `DevNexus.ApiService/Program.cs`
- **操作**:
  1. 添加 `AspNetCore.HealthChecks.NpgSql`
  2. 添加 `AspNetCore.HealthChecks.Redis`
  3. 配置 `/health` 和 `/health/ready` 端点

**Sprint 3 里程碑**: 性能优化完成，API 文档齐全，基础设施健壮

---

## 三、技术规范

### 编码标准

```csharp
// 1. 使用 file-scoped namespace
namespace DevNexus.Core.Services;

// 2. 所有异步方法使用 CancellationToken
public async Task<T> DoWorkAsync(CancellationToken cancellationToken = default)

// 3. 结构化日志格式
_logger.LogInformation("[Topic.Action] Message | Key={Value}", value);

// 4. DTO 使用 JsonPropertyName
[JsonPropertyName("sessionId")]
public Guid SessionId { get; set; }
```

### 分支策略

```
main          ← 生产分支
  └── dev     ← 开发分支
       ├── feature/signalr-backplane
       ├── feature/identity-auth
       └── feature/token-audit
```

### 测试要求

| 类型 | 覆盖目标 | 工具 |
|------|---------|------|
| 单元测试 | 核心服务 80% | xUnit + Moq |
| 集成测试 | API 端点 100% | WebApplicationFactory |

---


## 交付物清单

### Sprint 1 交付
- [ ] Redis SignalR 背板配置
- [ ] 多端消息广播功能
- [ ] ApproveAction/RejectAction API
- [ ] TokenAuditFilter + Seq 看板

### Sprint 2 交付
- [ ] ASP.NET Identity 集成
- [ ] 认证 API (Register/Login/Refresh/Logout)
- [ ] RefreshToken 实体与逻辑
- [ ] 客户端版本检查 API

### Sprint 3 交付
- [ ] ContextCacheService
- [ ] DistributedLockService
- [ ] RequestLoggingMiddleware
- [ ] Swagger 文档完善
- [ ] 健康检查端点

---

## 后续规划 (Phase 3-5)

完成本行动计划后，将进入以下阶段:

| 阶段 | 周期 | 核心任务 |
|------|------|---------|
| Phase 3 | Week 4-5 | OSS 封装、Artifact 推流、Hangfire Jobs、图表插件 |
| Phase 4 | Week 6-7 | Roslyn ScriptRunner、Live Console、Code RAG |
| Phase 5 | Week 8 | 全链路测试、性能调优、文档交付 |

---

**文档维护者**: DevNexus Backend Team  
**最后更新**: 2025年12月6日
