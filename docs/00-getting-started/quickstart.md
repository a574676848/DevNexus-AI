# 快速开始

本页提供一条最短可验证路径，用来确认当前项目的 API、客户端、聊天和文件链路可运行。

## 1. 准备依赖服务

确保 PostgreSQL、Redis、Seq、Qdrant 和 Elasticsearch 已可访问，并在 `src/common/DevNexus.AppHost` 中配置 User Secrets。配置方式见 [安装指南](./installation.md)。

## 2. 启动 AppHost

```bash
dotnet run --project src/common/DevNexus.AppHost
```

AppHost 负责启动 API 与 Web 客户端项目，并通过 Aspire 注入外部服务连接信息。

## 3. 打开客户端

优先使用 AppHost 输出的 Web 客户端地址。桌面客户端可独立启动：

```bash
dotnet run --project src/client/DevNexus.Client
```

## 4. 登录并配置模型供应商

进入设置页，至少配置一个 LLM Provider。聊天、Swarm、文档解析中的 Vision 能力都依赖可用模型供应商。

## 5. 验证聊天链路

1. 创建新会话。
2. 发送一条简单消息。
3. 确认流式输出、最终消息、会话列表和 SignalR 连接状态正常。

## 6. 验证文件链路

1. 上传一个文档或代码文件。
2. 确认生成 `UploadSession`、`FileAsset` 或解析状态。
3. 在聊天中引用该文件，确认语义上下文可用。

## 7. 验证文件任务链路

1. 创建 `FileTask`。
2. 在任务工作区提供 `runner.ps1` 或 `runner.py`。
3. 将结果写入 `outputs` 目录。
4. 确认结果文件回灌为新的文件资产。

## 继续阅读

- [第一步：理解当前产品能力边界](./first-steps.md)
- [用户指南](../03-guides/user-guide.md)
- [外部 Runner 合同](../03-guides/file-runner-contract.md)
- [API 规格说明](../04-api/api-specification.md)
