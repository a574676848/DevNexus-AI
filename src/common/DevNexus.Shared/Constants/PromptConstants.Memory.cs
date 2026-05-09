namespace DevNexus.Shared.Constants;

public static partial class PromptConstants
{
    public static class Memory
    {
        public const string UserProfileHeader = """

## 用户偏好（请严格遵守）
以下是关于用户的已知偏好，请在回答时考虑这些信息：
""";

        public const string EpisodicHeader = """

## 相关历史回忆
以下是与当前问题可能相关的历史对话摘要：
""";

        public const string UserFactExtractionPrompt = """
你是一个心理学与用户行为分析专家。你的任务是旁观用户与 AI 的对话，从中提取有价值的用户事实（User Facts），用于长期个性化服务。

【对话记录】
{0}

【萃取维度】
1. 技术栈与职业
2. 工作/代码偏好
3. 生活习惯与偏好
4. 重要实体与关系

【约束】
- 只提取明确或高置信信息；不确定则忽略。
- 不得编造。

【输出格式】(JSON Array)
[
        {{
    "category": "TechStack | Preference | LifeStyle | Entity",
    "content": "提取出的具体事实，使用精简客观陈述句"
        }}
]
请直接输出原生 JSON 文本，绝对不要使用 ```json 或 ``` 等任何 Markdown 代码块标记包裹，不要有任何前缀或后缀文字。
""";

        public const string MemoryConsolidationPrompt = """
你是记忆整合引擎。你需要合并【旧记忆记录】与【新识别特征】，并解决冲突。

【现存旧记忆】
{0}

【新识别特征】
{1}

【处理规则】
1. 新旧冲突时，以新信息为准，并标记旧事实过时。
2. 可补充时进行融合，生成更完整表述。
3. 删除语义重复条目。

【输出格式】(JSON)
{{
        "updatedFacts": [
                {{ "fact": "...", "category": "..." }}
        ],
        "obsoleteFactIdsToDelete": ["id1", "id2"]
}}
请直接输出原生 JSON 文本，绝对不要使用 ```json 或 ``` 等任何 Markdown 代码块标记包裹，不要有任何前缀或后缀文字。
""";

        public const string EpisodicSummaryPrompt = """
请用一句话总结以下对话核心内容（不超过100字）。
重点描述：用户问题、解决路径、关键结论。
如果无实质信息，请返回空字符串。

对话内容：
{0}

摘要：
""";

        public const string TechTagExtractionPrompt = """
从以下对话中提取技术关键词标签（最多5个）。
返回 JSON 数组格式，例如：["redis", "caching", "timeout"]。
若无明显技术关键词，返回 []。

对话内容：
{0}

标签：
请直接输出原生 JSON 文本，绝对不要使用 ```json 或 ``` 等任何 Markdown 代码块标记包裹，不要有任何前缀或后缀文字。
""";
    }
}
