namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// 表格分块 metadata 键定义。
/// 该组键由表格解析与知识库索引共同消费，属于 Infrastructure 内部协议。
/// </summary>
internal static class TableChunkMetadataKeys
{
    public const string SheetName = "SheetName";
    public const string RowStart = "RowStart";
    public const string RowEnd = "RowEnd";
    public const string RowCount = "RowCount";
    public const string ColumnCount = "ColumnCount";
    public const string HeadersJson = "HeadersJson";
    public const string HeadersText = "HeadersText";
    public const string ChunkLabel = "ChunkLabel";
}