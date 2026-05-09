# 开发环境搭建

本文档只保留当前仓库仍适用的本地开发信息。

## 基本要求

- .NET SDK 10
- Git
- 可选：Visual Studio 2022 / VS Code / Rider
- 可选：PostgreSQL、Redis、Qdrant、Seq、Elasticsearch、PaddleOCR 等外部依赖

## 关键入口

- 解决方案：`src/DevNexus.sln`
- Aspire AppHost：`src/common/DevNexus.AppHost`
- API：`src/backend/DevNexus.ApiService`
- Web：`src/client/DevNexus.Client.Web`
- Desktop：`src/client/DevNexus.Client`

## 恢复与构建

```bash
dotnet restore src/DevNexus.sln
dotnet build src/DevNexus.sln
```

## 推荐启动方式

```bash
dotnet run --project src/common/DevNexus.AppHost
```

如需单独启动客户端：

```bash
dotnet run --project src/client/DevNexus.Client.Web
```

或：

```bash
dotnet run --project src/client/DevNexus.Client
```

## 配置说明

- AppHost 使用 `src/common/DevNexus.AppHost` 中的参数与连接字符串配置。
- 具体连接名以 `src/common/DevNexus.AppHost/AppHost.cs` 和 `src/common/DevNexus.Shared/Constants` 为准。
- API 端开发配置以 `src/backend/DevNexus.ApiService` 下的 appsettings 为准。

## 验证点

启动后至少确认：

1. Swagger 可访问。
2. Chat、Artifact、Swarm 三个 Hub 可连接。
3. 客户端能正常登录并进入主界面。
