# 用户与认证架构

本文档描述当前用户与认证模块的分层边界。

## 分层职责

### Domain

`DevNexus.Domain` 保留领域实体、领域配置类型，以及面向领域的仓储和服务契约。用户认证链路中的 ASP.NET Identity 持久化模型不放在 Domain。

### Core

`DevNexus.Core` 承载认证与用户管理用例编排：

- `IAuthService`
- `IAuthApplicationService`
- `IUserManagementService`
- `IUserIdentityService`
- `IUserAdminApplicationService`
- 登录、刷新、退出、修改资料、用户管理等命令和查询处理器

这些契约描述应用流程和基础设施边界，不属于纯领域规则。

### Infrastructure

`DevNexus.Infrastructure` 实现认证相关基础设施：

- ASP.NET Identity 集成
- `InfrastructureUser`
- `ApplicationDbContext`
- `UserIdentityService`
- `JwtTokenService`
- Refresh Token 持久化

直接依赖 `UserManager`、`RoleManager`、EF Core 或 JWT 库的实现都位于 Infrastructure。

### ApiService

API 层负责 HTTP 适配：

- 接收请求 DTO。
- 解析当前用户。
- 调用 Core 用例。
- 返回 HTTP 响应。

控制器不直接承载 Identity 编排或 EF 查询。

### Client

客户端拆分为三类职责：

- `Services/*`：REST、SignalR、认证状态等传输逻辑。
- `AuthenticationStateProvider`：认证状态桥接。
- Blazor 组件：展示和交互。

组件不重复实现 Token 解析、刷新或退出协议。

## 当前认证行为

- 登录成功后返回 Access Token 与 Refresh Token。
- Refresh Token 服务端只保存哈希。
- Refresh Token 每次刷新都会轮换。
- Logout 会撤销当前 Refresh Token。
- Logout all devices 会撤销用户所有未撤销 Refresh Token。
- JWT 用户标识同时写入 `sub` 和 `ClaimTypes.NameIdentifier`，兼容客户端解析。

## 维护规则

- DTO、HTTP 语义和跨基础设施编排放在 Core。
- Identity、JWT、EF Core 和外部 SDK 依赖放在 Infrastructure。
- 控制器只做请求适配、当前用户解析和响应映射。
- 客户端异步认证逻辑必须支持取消和释放。
