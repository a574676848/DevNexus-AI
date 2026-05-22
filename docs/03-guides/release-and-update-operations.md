# 版本发布与客户端更新操作手册

本文档说明桌面客户端上线发布的标准流程，以及 GitHub Actions、GitHub Releases、版本中心三者之间的关系。

## 发布目标

- Windows 客户端发布真实安装器
- macOS 客户端发布真实安装包
- 自动生成可导入版本中心的 `release-metadata.json`
- 统一通过 GitHub Releases 提供下载地址

## 当前入口

### 管理端

- `/settings/release-center`

### 服务端接口

- `/api/v1/admin/releases`
- `/api/v1/admin/rollouts`
- `/api/update/manifest`
- `/api/update/events`

### GitHub Workflow

- [desktop-client.yml](../../.github/workflows/desktop-client.yml)

## 平台差异

### Windows

- 客户端更新链路依赖 `DevNexus.Client.Updater.exe`
- 更新包类型应为 `installer`
- 最终分发包应是可直接执行的 Windows 安装器，如 `.exe`

### macOS

- 客户端更新链路不走 Windows updater
- 下载完成后交给系统打开安装包
- 更新包类型应为 `pkg` 或 `dmg`
- 最终分发包应是可直接打开的 macOS 安装包

结论：

- Windows 和 macOS 不能共用同一种发布包
- 只能共用同一套发布平台和 metadata 结构

## 标准上线流程

1. 准备版本号，例如 `v1.2.0`
2. 推送 tag 或在 GitHub 发布 release
3. GitHub Actions 构建桌面客户端
4. GitHub Actions 上传发布资产到 GitHub Releases
5. GitHub Actions 生成 `release-metadata.json`
6. 在版本中心导入 `release-metadata.json`
7. 确认版本与投放规则生效
8. 在运行看板查看更新状态与异常

## release-metadata.json 用途

这份文件用于把 GitHub 构建产物直接导入版本中心。

应包含：

- `version`
- `channel`
- `title`
- `releaseNotes`
- `artifacts`
- `rolloutTemplate`
- `publishRelease`
- `createRollout`

其中 `artifacts` 的 `downloadUrl` 应直接指向 GitHub Releases 资产下载地址。

## GitHub Releases 资产要求

### Windows 资产

- Windows 安装器
- Windows updater
- `release-metadata.json`

### macOS 资产

- macOS 安装包
- `release-metadata.json`

## 上线前检查

1. Windows 是否输出真实安装器，而不是仅 publish 目录或 zip
2. macOS 是否输出真实 `.pkg` 或 `.dmg`
3. GitHub Releases 下载地址是否可访问
4. `release-metadata.json` 中的文件名、大小、checksum 是否匹配
5. 版本中心导入后是否成功生成 Release
6. 若启用自动投放，是否成功生成 Rollout
7. 客户端 manifest 是否能返回正确版本

## 关键确认项

发布流程中需逐项确认：

1. Windows 安装器真实打包步骤
2. macOS 安装包真实打包步骤
3. 发布资产文件名与客户端消费规则完全对齐

## 排障重点

### 导入失败

- 检查 metadata JSON 是否为空
- 检查 JSON 结构是否符合 `ImportReleaseMetadataRequest`
- 检查 `artifacts` 是否为空

### 客户端不更新

- 检查 Release 是否存在且已发布
- 检查 Rollout 是否存在且已启用
- 检查 `platform`、`architecture`、`channel` 是否匹配
- 检查 `downloadUrl` 是否可访问

### Windows 安装失败

- 检查 `packageType` 是否为 `installer`
- 检查安装器文件是否为真实 `.exe`
- 检查 updater 是否随安装包一起发布

### macOS 安装失败

- 检查 `packageType` 是否为 `pkg` 或 `dmg`
- 检查安装包是否可被系统直接打开

