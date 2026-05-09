# Auth Token 模型

本文档描述当前访问令牌与刷新令牌行为。

## Access Token

- 格式：JWT。
- 生成服务：`JwtTokenService`。
- 用户标识 Claim：`sub`、`ClaimTypes.NameIdentifier`。
- 角色 Claim：`ClaimTypes.Role`。
- 默认校验：Issuer、Audience、Lifetime、SigningKey。

客户端同时兼容标准 `sub` 与 `ClaimTypes.NameIdentifier`，避免不同解析方式导致用户 ID 丢失。

## Refresh Token

- 使用加密安全随机值生成。
- 服务端只持久化 Token 哈希。
- 明文 Refresh Token 只返回给客户端，不写入数据库。
- 元数据包含用户、设备、IP、User-Agent、创建时间、过期时间、撤销状态和替换 Token 关系。

## 轮换行为

刷新流程：

1. 客户端提交当前 Refresh Token。
2. 服务端计算哈希并查询元数据。
3. 服务端拒绝已撤销或已过期 Token。
4. 服务端生成新的 Access Token 和 Refresh Token。
5. 旧 Refresh Token 标记为已撤销，并记录替换 Token ID。
6. 客户端保存新的令牌对。

## 复用检测

已撤销 Refresh Token 再次提交时，服务端拒绝请求并记录警告日志。该行为用于发现异常复用或客户端状态不同步。

## 退出行为

### Logout

- 客户端调用 `POST /api/v1/auth/logout`。
- 服务端撤销当前 Refresh Token。
- 客户端清理本地认证状态。

### Logout all devices

- 客户端调用 `POST /api/v1/auth/logout-all`。
- 服务端撤销该用户所有未撤销 Refresh Token。
- 客户端清理本地认证状态。
