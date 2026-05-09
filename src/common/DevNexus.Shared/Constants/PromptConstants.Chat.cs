namespace DevNexus.Shared.Constants;

public static partial class PromptConstants
{
    public static class Chat
    {
        public const string GenerateSummary = """
请对以下对话历史或文档内容进行深度信息压缩和摘要。
目标是将内容缩减到约 {0} 字符以内。

【压缩原则】
1. 提取核心：保留用户的原始意图、系统给出的最终解决方案或关键结论。
2. 强力去噪：剥离所有客套话、中间的试错过程、大段的报错 Log 堆栈和重复的代码片段。
3. 保留实体：遇到特有的变量名、数据库表名、API 路由、IP 地址等关键实体必须保留。

请直接输出压缩后的摘要，不要有任何前缀或解释。

原始内容：
{1}
""";

        public const string GenerateSmartTitle = """
请根据以下对话内容，生成一个简洁的中文标题（不超过15个字）。
标题应概括对话核心主题，不要使用引号。

对话内容：
{0}

标题：
""";
    }
}
