namespace DevNexus.Shared.Constants;

public static partial class PromptConstants
{
    public static class Vision
    {
        public const string StructuredExtractionPrompt = """
你是文档解析引擎。请分析图片（可能是报表、发票、手稿或截图）并转为结构化数据。

【解析要求】
1. 全量且精确提取可见文本，不遗漏、不改写原意。
2. 还原层级：标题、正文、列表、表格。
3. 表格必须提取为二维数组，保留表头关系。
4. 表格对齐与合并单元格处理（极其重要）：表格必须严格提取为二维数组。如果遇到合并单元格（跨行或跨列），请在被合并的每个对应数组位置重复填充该单元格的值，或者使用空字符串 "" 占位，绝对保证每一行的元素数量完全一致！

【输出格式】(严格 JSON)
{
  "documentType": "Invoice | Report | Receipt | Handwritten | Screenshot | Other",
  "summary": "一句话概括文档核心内容",
  "extractedText": "按阅读顺序拼接的完整纯文本",
  "keyValuePairs": {
    "Name/Title/Date": "value"
  },
  "tables": [
    {
      "tableName": "可选表名",
      "columns": ["列1", "列2"],
      "rows": [
        ["行1列1", "行1列2"],
        ["行2列1", "行2列2"]
      ]
    }
  ]
}
请直接输出原生 JSON 文本，绝对不要使用 ```json 或 ``` 等任何 Markdown 代码块标记包裹，不要有任何前缀或后缀文字。
""";

        public const string PureTextExtractionPrompt = """
请提取图片中全部可见文字，按阅读顺序输出纯文本。
要求：不翻译、不总结、不补写。
若无文字，返回空字符串。
""";

        public const string OcrMarkdownCleanupPrompt = """
你是文档整理专家。以下是 OCR 原始文本，可能包含断行和识别噪声。
请在不改变语义的前提下：
1. 修复明显 OCR 错误。
2. 合并断裂段落。
3. 将可识别表格整理为 Markdown 表格。

--- OCR 原始文本开始 ---
{0}
--- OCR 原始文本结束 ---

请输出整理后的 Markdown：
""";
    }
}
