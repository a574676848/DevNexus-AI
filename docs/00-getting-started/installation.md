# 安装指南

本文档说明当前仓库的本地运行前提、构建命令和启动入口。

## 基本要求

- .NET SDK 10
- Git
- PostgreSQL
- Redis
- Seq
- Qdrant
- Elasticsearch
- 可选：PaddleOCR 服务

开发工具可选择 Visual Studio 2022、Rider 或 VS Code。

## 获取代码

```bash
git clone https://github.com/a574676848/DevNexus-AI.git
cd DevNexus-AI
```

## 恢复与构建

```bash
dotnet restore src/DevNexus.sln
dotnet build src/DevNexus.sln
```

解决方案包含 API、Core、Domain、Infrastructure、Shared、AppHost、Blazor WebAssembly、MAUI 客户端和 Updater 项目。

## 配置连接串和密钥

推荐在 AppHost 项目中使用 User Secrets：

```bash
cd src/common/DevNexus.AppHost
dotnet user-secrets init
dotnet user-secrets set "Parameters:jwt-key" "replace-with-a-strong-jwt-key"
dotnet user-secrets set "Parameters:encryption-key" "replace-with-base64-aes-key"
dotnet user-secrets set "Parameters:encryption-iv" "replace-with-base64-aes-iv"
dotnet user-secrets set "ConnectionStrings:devnexus" "Host=localhost;Port=5432;Database=devnexus;Username=postgres;Password=CHANGE_ME"
dotnet user-secrets set "ConnectionStrings:redis" "localhost:6379"
dotnet user-secrets set "ConnectionStrings:seq" "http://localhost:5341"
dotnet user-secrets set "ConnectionStrings:qdrant" "http://localhost:6333"
dotnet user-secrets set "ConnectionStrings:elasticsearch" "http://localhost:9200"
```

`paddle-ocr` 连接串只在启用 OCR 服务时需要配置。

## 推荐启动方式

```bash
dotnet run --project src/common/DevNexus.AppHost
```

AppHost 会启动 API 与 Web 客户端项目，并把连接串、JWT 参数和加密参数注入 API 服务。实际监听地址以控制台输出为准。

桌面客户端可以单独启动：

```bash
dotnet run --project src/client/DevNexus.Client
```

## 验证点

1. API 在开发环境提供 `/swagger`。
2. `/health` 和 `/alive` 可返回健康状态。
3. Web 或桌面客户端可以登录。
4. 新建聊天后可以收到流式响应。
5. 文件上传后能看到语义解析或文件任务状态。
