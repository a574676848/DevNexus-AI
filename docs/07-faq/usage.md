# 常见问题 - 使用与排障

## 上传后推荐的操作顺序是什么？

推荐顺序如下：

1. 上传文件。
2. 等待上传 finalize，形成 FileAsset。
3. 如果需要让系统理解内容，可触发语义解析。
4. 如果需要真实生成新文件，可创建 FileTask。
5. 查看结果卡，把输出资产继续加入下一轮。

## 文件解析失败后在哪里看真实原因？

现在前端会直接展示后端回传的真实失败原因，位置通常有三处：

1. 附件 chip 的失败提示。
2. 输入区上下文 notices。
3. 放大输入框中的同类状态提示。

如果只看到泛化报错，通常说明前端没有收到 Artifact Hub 推送，或者后端没有返回具体错误消息。

## 上传成功但没有生成结果文件怎么办？

按这个顺序检查：

1. 上传会话是否已经 finalize。
2. 是否已经成功创建 FileTask。
3. FileTask 是否进入失败或完成状态。
4. 任务工作区里是否存在 runner.ps1 或 runner.py。
5. runner 是否在 outputs 目录写入了结果文件。
6. 结果文件是否通过平台校验。

常见误区是“上传成功”被误认为“任务已经执行完”。这两者不是一回事。

## FileTask 失败后怎么恢复？

如果任务失败：

1. 先查看任务状态和失败摘要。
2. 修复 runner、输入文件或指令问题。
3. 使用任务结果区的重试能力重新执行。

如果问题是输入文件本身不完整，建议直接重新上传后再发起新任务，而不是盲目重试旧任务。

## 为什么有时只得到摘要文件，没有真正结果？

这是平台的回退行为。

当任务工作区里没有可执行的 runner.ps1 或 runner.py 时，平台不会猜测如何处理真实文件，而是生成任务摘要作为可见回退结果。

这说明平台调度正常，但外部执行器没有接上。

## 如何把结果继续交给智能体处理？

结果卡支持把新生成的资产继续加入下一轮上下文。这样新的对话或任务会引用新结果，而不是继续引用旧输入。

这一步是当前文件平台闭环中的关键动作。

## SignalR 断开会有什么影响？

会直接影响三类体验：

1. 聊天流式输出不完整。
2. 文件解析状态不更新。
3. Swarm 工作包状态和 Agent 状态不刷新。

如果 REST 正常但界面一直不动，优先检查 /chat-hub、/artifact-hub、/swarm-hub 是否可连接。

## 文件处理一定要走 Excel 专用功能吗？

不是。

当前平台使用通用文件运行时，不要求平台内部内置 Excel 专用流程。你可以用 Python、PowerShell 或其他外部执行器去处理 Excel、PDF、图片或其他文件。

## 什么时候应该重新上传文件，而不是直接重试任务？

如果出现以下情况，优先重新上传：

1. 原文件损坏。
2. 上传内容不完整。
3. 你实际上换了输入版本。
4. 模板文件本身已经变化。

如果只是 runner 逻辑有 bug、路径写错、依赖缺失，则更适合修复后直接重试任务。

## 响应慢应该怎么判断是哪一层的问题？

可以按层排查：

1. 聊天慢: 看模型供应商和 Chat Hub。
2. 解析慢: 看后台解析任务和 Artifact 状态推送。
3. 文件任务慢: 看 runner 执行时长和 outputs 产出。
4. 整体都慢: 看 Redis、数据库、对象存储和服务器资源。

## 数据备份与恢复（独立运维操作）

### 备份

**数据库备份:**
```bash
pg_dump -h localhost -U postgres devnexus > backup.sql
```

**向量库备份:**
```bash
# Qdrant - 使用快照功能
curl -X POST http://localhost:6333/collections/system-experience/snapshots
```

### 恢复

**数据库恢复:**
```bash
psql -h localhost -U postgres devnexus < backup.sql
```

**向量库恢复:**
```bash
# 使用 Qdrant 恢复 API
curl -X PUT http://localhost:6333/collections/system-experience/snapshots/recover \
  -d '{"snapshot_name": "your-snapshot"}'
```

---

## 其他问题

### 如何反馈问题？

1. 在 [GitHub Issues](https://github.com/a574676848/DevNexus-AI/issues) 中提交
2. 提供详细的错误信息
3. 附上相关日志

### 如何获取帮助？

1. 查看 [文档](../)
2. 搜索 [GitHub Issues](https://github.com/a574676848/DevNexus-AI/issues)
3. 在 [讨论区](https://github.com/a574676848/DevNexus-AI/discussions) 提问

---

**没有找到答案？** → [提交 Issue](https://github.com/a574676848/DevNexus-AI/issues)
