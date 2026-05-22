namespace DevNexus.Core.Models.Cli;

/// <summary>
/// CLI 运行时输入信封。
/// </summary>
public sealed record CliRuntimeInputEnvelope(
    string Input,
    string ModelVisiblePreview,
    int OriginalLength,
    bool IsBlankLine);
