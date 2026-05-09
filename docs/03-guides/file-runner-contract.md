# DevNexus AI 外部 Runner 合同

## 1. 目标

FileTask 运行时已经收敛为平台编排 + 外部执行器模式。

平台不再在内部长期维护某一种文件格式的强业务逻辑，而是通过任务工作区和合同文件，把真实文件处理委托给外部 runner。

当前优先支持两种入口：

1. runner.ps1
2. runner.py

如果两者都不存在，平台会进入回退模式，生成任务摘要文件而不是执行真实文件处理。

![外部任务执行器架构](../assets/Runner_Execution_Architecture.png)

## 2. 执行优先级

平台在任务工作区中的执行顺序如下：

1. 如果存在 runner.ps1，优先执行 PowerShell runner。
2. 否则如果存在 runner.py，执行 Python runner。
3. 否则进入回退模式，输出 task-summary.md。

## 3. 工作区结构

典型任务工作区会包含以下目录和文件：

```text
task-workspace/
├─ inputs/
│  ├─ sources/
│  └─ templates/
├─ outputs/
├─ task-execution-contract.json
├─ runner.ps1 或 runner.py
```

说明:

- inputs/sources 存放待处理输入资产副本。
- inputs/templates 存放模板或参考资产副本。
- outputs 用于写入最终结果。
- task-execution-contract.json 提供任务元信息和输入清单。

## 4. 合同文件职责

task-execution-contract.json 至少承担这些作用：

1. 提供 fileTaskId、sessionId、taskType 等任务标识。
2. 提供输入资产和模板资产的清单。
3. 提供 instructions 等业务指令文本。
4. 告诉 runner 当前工作区下哪些目录可读、哪些目录应写出结果。

runner 应该始终以这个合同文件为主输入，而不是写死路径假设。

## 5. Runner 输入约定

runner 实现时应遵守以下原则：

1. 只读取当前任务工作区内的文件。
2. 从合同文件中解析任务类型、输入资产和模板资产。
3. 不依赖平台外部的任意绝对路径。
4. 不修改 inputs 原始副本，必要时复制后处理。

## 6. Runner 输出约定

runner 应把结果写到 outputs 目录。

建议遵循以下约定：

1. 真正需要交付给用户的新文件放在 outputs 根目录或其明确子目录。
2. 可选的调试信息、报告或日志也可以写在 outputs 中，但应与主结果区分命名。
3. 如果没有任何真实结果，至少写出一个说明性文件，帮助定位失败原因。

平台随后会扫描 outputs，并尝试把有效输出回灌为新 FileAsset。

## 7. 校验与回灌

平台不会盲目接受所有输出文件。回灌前会做基础结构校验，例如：

1. JSON 是否可解析。
2. XML 是否结构合法。
3. 表格或文档文件是否具备基本可读性。
4. 通用文件是否非空。

只有通过校验的输出才会稳定成为结果资产。

## 8. 错误处理建议

runner 处理失败时，建议做到：

1. 返回明确错误。
2. 在 outputs 中写入可读的失败说明文件，便于排查。
3. 避免静默失败后只留下空目录。

如果 runner 完全缺失，平台会生成 task-summary.md 作为回退结果，这通常意味着执行器尚未接入，而不是任务业务本身已完成。

## 9. 适配建议

### 9.1 Python runner

适合：

- Excel、CSV、PDF、图像、数据清洗
- 需要 pandas、openpyxl、python-docx 等生态库的场景

建议：

- 在 runner.py 中显式读取合同文件。
- 将依赖安装与虚拟环境管理纳入部署流程，而不是依赖平台临时安装。

### 9.2 PowerShell runner

适合：

- Windows 环境下的自动化脚本
- 文件搬运、批处理、系统命令封装

建议：

- 注意执行策略和运行账户权限。
- 把结果统一写入 outputs，而不是散落到其他目录。

### 9.3 其他外部执行器适配

其他外部执行器也沿用这套合同：

1. 平台仍负责工作区和合同准备。
2. opencodecli 只作为新的执行器实现。
3. 不要绕过 FileTask 平台直接写散乱的临时文件链路。

## 10. 最小实现建议

一个可工作的最小 runner 应至少做到：

1. 读取 task-execution-contract.json。
2. 识别 inputs/sources 和 inputs/templates。
3. 根据 instructions 执行处理。
4. 在 outputs 写出一个真实结果文件。
5. 失败时给出明确错误说明。
