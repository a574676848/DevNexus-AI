# 贡献指南

感谢你关注 DevNexus AI。提交代码前，请先阅读：

- [开发环境](./docs/06-development/setup.md)
- [编码规范](./docs/06-development/coding-standards.md)
- [测试与校验](./docs/06-development/testing.md)
- [详细贡献说明](./docs/06-development/contributing.md)

## 基本流程

1. Fork 仓库并创建目标清晰的分支。
2. 保持改动聚焦，避免夹带无关重构。
3. 本地执行必要的恢复、构建和验证命令。
4. 同步更新受影响的 README、docs 或示例说明。
5. 提交 Pull Request，并说明改动动机、验证结果和已知风险。

## 提交前检查

```bash
dotnet restore src/DevNexus.sln
dotnet build src/DevNexus.sln
```

涉及接口、聊天、文件、更新或供应商管理的改动，需要补充对应的手动验证说明。
