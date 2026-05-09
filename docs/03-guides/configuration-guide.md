# DevNexus AI 配置指南

本文档说明当前代码使用的主要配置入口。配置事实源包括 `src/common/DevNexus.AppHost/AppHost.cs`、`src/backend/DevNexus.ApiService/Program.cs`、`src/backend/DevNexus.ApiService/appsettings.json` 和各配置模型。

## 配置入口

| 入口 | 用途 |
|------|------|
| `src/common/DevNexus.AppHost/appsettings.json` | AppHost 非敏感参数默认值 |
| `src/backend/DevNexus.ApiService/appsettings.json` | API 默认配置模板 |
| User Secrets | 本地开发密钥与连接串 |
| 环境变量 | 部署环境配置 |

推荐优先通过 AppHost 注入配置，而不是直接修改仓库内的 `appsettings.json`。

## AppHost 参数

AppHost 使用 Aspire `AddParameter` 管理敏感参数：

```bash
dotnet user-secrets set "Parameters:jwt-key" "replace-with-a-strong-jwt-key"
dotnet user-secrets set "Parameters:encryption-key" "replace-with-base64-aes-key"
dotnet user-secrets set "Parameters:encryption-iv" "replace-with-base64-aes-iv"
```

对应注入到 API 的配置键：

- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Encryption__Key`
- `Encryption__IV`

## 连接串

AppHost 通过 `AddConnectionString` 读取外部服务连接串：

```bash
dotnet user-secrets set "ConnectionStrings:devnexus" "Host=localhost;Port=5432;Database=devnexus;Username=postgres;Password=CHANGE_ME"
dotnet user-secrets set "ConnectionStrings:redis" "localhost:6379"
dotnet user-secrets set "ConnectionStrings:seq" "http://localhost:5341"
dotnet user-secrets set "ConnectionStrings:qdrant" "http://localhost:6333"
dotnet user-secrets set "ConnectionStrings:elasticsearch" "http://localhost:9200"
dotnet user-secrets set "ConnectionStrings:paddle-ocr" "http://localhost:5433"
```

`paddle-ocr` 是可选项，仅在启用 OCR 服务时需要。

## 基础设施依赖

| 组件 | 当前用途 |
|------|----------|
| PostgreSQL | 业务数据、Identity、Provider、聊天、文件资产、审计与后台任务 |
| Redis | 分布式缓存与 SignalR 背板 |
| Seq | 结构化日志 |
| Qdrant | 向量检索 |
| Elasticsearch | 会话与消息搜索 |
| S3 兼容存储或本地存储 | 原始文件和生成文件 |
| PaddleOCR | 图片 / PDF OCR 辅助解析 |

当前仓库不提供根目录 `docker-compose.yml`。依赖服务的启动方式由本地环境或部署平台决定。

## JWT 配置

```json
{
  "Jwt": {
    "Key": "YOUR_SECURE_JWT_KEY_AT_LEAST_32_CHARACTERS_LONG",
    "Issuer": "DevNexus",
    "Audience": "DevNexus",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 30
  }
}
```

`JwtOptions.Validate()` 会在 API 启动时校验密钥长度，配置无效时阻止启动。

## 加密配置

```json
{
  "Encryption": {
    "Key": "BASE64_AES_256_KEY",
    "IV": "BASE64_AES_IV"
  }
}
```

该配置用于 Provider API Key、对象存储密钥等敏感字段的加密存储。API 启动时会校验加密配置，配置无效会终止启动。

## 存储配置

`Storage` 配置决定文件存储默认模式：

```json
{
  "Storage": {
    "Provider": "S3",
    "Local": {
      "RootPath": "wwwroot/uploads",
      "BaseUrl": "/uploads"
    },
    "S3": {
      "AccessKeyId": "CHANGE_ME",
      "SecretAccessKey": "CHANGE_ME",
      "ServiceUrl": "https://s3.amazonaws.com",
      "BucketName": "devnexus-files",
      "Region": "us-east-1",
      "ForcePathStyle": false,
      "CdnDomain": "",
      "UseHttps": true,
      "PresignedUrlExpirationSeconds": 3600
    }
  }
}
```

生产环境优先使用 S3 兼容存储。本地模式适合开发和功能验证。

## 文件运行时配置

文件任务由平台创建和调度，格式特化处理由受控工作区中的外部 Runner 完成：

- `runner.ps1`
- `runner.py`
- `task-execution-contract.json`
- `inputs/`
- `templates/`
- `outputs/`

详细约定见 [外部 Runner 合同](./file-runner-contract.md)。

## SignalR 配置

API 当前映射三个 Hub：

- `/chat-hub`
- `/artifact-hub`
- `/swarm-hub`

Redis 连接串存在时，SignalR 会启用 Redis 背板并使用 `DevNexus` 作为 Channel Prefix。

## 可观测性配置

```json
{
  "Monitoring": {
    "EnableDetailedMonitoring": true,
    "TrackProviderUsage": true,
    "TrackTokenConsumption": true,
    "TrackVectorDbSize": true,
    "MetricsCollectionIntervalSeconds": 60,
    "EnablePerformanceProfiling": false,
    "SlowQueryThresholdMs": 1000
  }
}
```

Seq、OpenTelemetry、审计分析和 Token 统计共同构成当前观测链路。

## 生产环境配置方式

部署环境使用环境变量注入敏感配置：

```bash
export Jwt__Key="YOUR_PRODUCTION_JWT_KEY_32_CHARS_MINIMUM"
export Jwt__Issuer="DevNexus"
export Jwt__Audience="DevNexus"
export Encryption__Key="BASE64_AES_256_KEY"
export Encryption__IV="BASE64_AES_IV"
export ConnectionStrings__devnexus="Host=prod-db;Port=5432;Database=devnexus;Username=app_user;Password=CHANGE_ME"
export ConnectionStrings__redis="prod-redis:6379,password=CHANGE_ME"
export ConnectionStrings__seq="http://prod-seq:5341"
export ConnectionStrings__qdrant="http://prod-qdrant:6333"
export ConnectionStrings__elasticsearch="http://prod-elasticsearch:9200"
```

## 配置校验清单

- JWT 密钥长度满足要求。
- 加密 Key / IV 为有效 Base64。
- PostgreSQL、Redis、Seq、Qdrant、Elasticsearch 可访问。
- `Storage.Provider` 与实际存储配置一致。
- 生产环境不使用仓库模板中的占位密钥。
- 文件任务工作区具备必要的 Runner 和输出目录权限。
