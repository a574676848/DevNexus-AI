# 贡献说明

如需参与当前仓库开发，建议遵循下面的最小流程。

## 基本流程

1. 拉取最新代码。
2. 基于目标修改创建分支。
3. 完成开发后先构建解决方案。
4. 对受影响功能做最小可验证的冒烟检查。
5. 更新必要文档后再提交。

## 提交前至少确认

- `dotnet restore src/DevNexus.sln`
- `dotnet build src/DevNexus.sln`
- 受影响页面、接口或主流程已手动验证
- 若改动了文档，内容与当前代码一致

## 相关文档

- `setup.md`
- `coding-standards.md`
- `testing.md`
- `../CONTRIBUTING.md`
