using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Hangfire;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using DevNexus.Infrastructure.Services.Parsing;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 文档解析后台任务
/// </summary>
public class DocumentParsingJob
{
    private readonly ILogger<DocumentParsingJob> _logger;
    private readonly ISmartDocumentParser _parserFactory;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IFileStorageService _storageService;
    private readonly IArtifactStatusPublisher _statusPublisher;
    private readonly IClientNotifier _clientNotifier;
    private readonly FileMimeValidationService _mimeValidationService;

    public DocumentParsingJob(
        ILogger<DocumentParsingJob> logger,
        ISmartDocumentParser parserFactory,
        IKnowledgeBaseService knowledgeBase,
        IFileStorageService storageService,
        IArtifactStatusPublisher statusPublisher,
        IClientNotifier clientNotifier,
        FileMimeValidationService mimeValidationService)
    {
        _logger = logger;
        _parserFactory = parserFactory;
        _knowledgeBase = knowledgeBase;
        _storageService = storageService;
        _statusPublisher = statusPublisher;
        _clientNotifier = clientNotifier;
        _mimeValidationService = mimeValidationService;
    }

    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(
        string traceId,
        string fileName,
        string fileUrl,
        string userId,
        ParsingOptions options,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var downloadStopwatch = new Stopwatch();
        var parseStopwatch = new Stopwatch();
        var indexStopwatch = new Stopwatch();
        string currentStage = "startup";

        _logger.LogInformation("Job Started: Parse Document {FileName} (TraceId: {TraceId})", fileName, traceId);

        if (Guid.TryParse(userId, out var parsedUserId) && sessionId.HasValue)
        {
            await _clientNotifier.NotifyThinkingAsync(
                parsedUserId,
                sessionId.Value,
                string.Format(SmartDocumentConstants.ParsingThinking.StartTemplate, fileName),
                metadata: new Dictionary<string, object>
                {
                    [SmartDocumentConstants.ParsingThinking.SourceMetadataKey] = SmartDocumentConstants.ParsingThinking.SourceName
                });
        }

        await NotifyStatusAsync(userId, traceId, ArtifactStatusConstants.Pending, null);
        await NotifyStatusAsync(userId, traceId, ArtifactStatusConstants.Processing, null);

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{traceId}_{Guid.NewGuid()}");
        FileMimeValidationResult? mimeValidationResult = null;

        try
        {
            currentStage = "download";
            downloadStopwatch.Start();
            _logger.LogInformation("Downloading file from {FileUrl}...", fileUrl);
            using (var downloadStream = await _storageService.DownloadFileAsync(fileUrl, cancellationToken))
            using (var tempFileStream = File.Create(tempFilePath))
            {
                await downloadStream.CopyToAsync(tempFileStream, cancellationToken);
            }
            downloadStopwatch.Stop();
            var downloadedSize = new FileInfo(tempFilePath).Length;

            currentStage = "mime-validation";
            var fileHeadBytes = new byte[Math.Min(8192, downloadedSize > int.MaxValue ? 8192 : (int)Math.Max(1, downloadedSize))];
            await using (var headStream = File.OpenRead(tempFilePath))
            {
                _ = await headStream.ReadAsync(fileHeadBytes, cancellationToken);
            }

            mimeValidationResult = _mimeValidationService.Validate(fileName, options.DeclaredMimeType, fileHeadBytes);
            if (!mimeValidationResult.IsValid)
            {
                throw new InvalidDataException(mimeValidationResult.ErrorMessage ?? "服务端 MIME 校验失败");
            }

            _logger.LogInformation(
                "[ParsePipeline] Downloaded file and passed MIME validation | TraceId={TraceId} FileName={FileName} Bytes={Bytes} DownloadMs={DownloadMs} EffectiveMime={EffectiveMime}",
                traceId,
                fileName,
                downloadedSize,
                downloadStopwatch.ElapsedMilliseconds,
                mimeValidationResult.EffectiveMimeType);

            SmartDocument doc;
            currentStage = "parse";
            parseStopwatch.Start();
            using (var processingStream = File.OpenRead(tempFilePath))
            {
                var context = new ParsingContext
                {
                    TraceId = traceId,
                    SessionId = sessionId?.ToString(),
                    UserId = userId,
                    CancellationToken = cancellationToken
                };

                doc = await _parserFactory.ParseAsync(
                    processingStream,
                    fileName,
                    mimeValidationResult.EffectiveMimeType,
                    options,
                    context);

                doc.Metadata[SmartDocumentConstants.MetadataKeys.SourceUrl] = fileUrl;
                doc.Metadata[SmartDocumentConstants.MetadataKeys.FileSize] = new FileInfo(tempFilePath).Length;
                doc.Metadata[SmartDocumentConstants.MetadataKeys.UploadedAt] = DateTime.UtcNow;
                doc.Metadata[SmartDocumentConstants.MetadataKeys.EffectiveMimeType] = mimeValidationResult.EffectiveMimeType;
                if (!string.IsNullOrWhiteSpace(options.DeclaredMimeType))
                {
                    doc.Metadata[SmartDocumentConstants.MetadataKeys.DeclaredMimeType] = options.DeclaredMimeType!;
                }

                if (doc.Content is ImageDocumentContent imageContent && string.IsNullOrEmpty(imageContent.Url))
                {
                    imageContent.Url = fileUrl;
                    _logger.LogDebug("[DocumentParsingJob] Set image URL | FileUrl={FileUrl}", fileUrl);
                }
            }
            parseStopwatch.Stop();

            doc.Metadata[SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = SmartDocumentConstants.SemanticPipelineStages.Parsed;
            doc.Metadata[SmartDocumentConstants.MetadataKeys.SemanticParsedAt] = DateTime.UtcNow;
            await NotifyStatusAsync(userId, traceId, ArtifactStatusConstants.Parsed, doc);

            if (Guid.TryParse(userId, out var completedUserId) && sessionId.HasValue)
            {
                await _clientNotifier.NotifyThinkingAsync(
                    completedUserId,
                    sessionId.Value,
                    SmartDocumentConstants.ParsingThinking.CompletedMessage,
                    metadata: new Dictionary<string, object>
                    {
                        [SmartDocumentConstants.ParsingThinking.SourceMetadataKey] = SmartDocumentConstants.ParsingThinking.SourceName
                    });
            }

            try
            {
                currentStage = "index";
                indexStopwatch.Start();
                doc.Metadata[SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = SmartDocumentConstants.SemanticPipelineStages.Indexing;
                doc.Metadata[SmartDocumentConstants.MetadataKeys.VectorIndexStatus] = SmartDocumentConstants.VectorIndexStatuses.Building;
                await NotifyStatusAsync(userId, traceId, ArtifactStatusConstants.Indexing, doc);

                if (Guid.TryParse(userId, out var kbUserId))
                {
                    await _knowledgeBase.UpsertDocumentAsync(doc, kbUserId, cancellationToken);
                }
                else
                {
                    await _knowledgeBase.UpsertDocumentAsync(doc, cancellationToken);
                }
                indexStopwatch.Stop();
                doc.Metadata[SmartDocumentConstants.MetadataKeys.VectorIndexStatus] = SmartDocumentConstants.VectorIndexStatuses.Ready;
                doc.Metadata[SmartDocumentConstants.MetadataKeys.VectorIndexedAt] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert document to Knowledge Base (RAG): {FileId}", doc.FileId);
                doc.Metadata[SmartDocumentConstants.MetadataKeys.VectorIndexStatus] = SmartDocumentConstants.VectorIndexStatuses.Failed;
                doc.Metadata[SmartDocumentConstants.MetadataKeys.VectorIndexError] = ex.Message;
            }

            doc.Metadata[SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = SmartDocumentConstants.SemanticPipelineStages.Completed;
            await NotifyStatusAsync(userId, traceId, ArtifactStatusConstants.Completed, doc);

            _logger.LogInformation(
                "[ParsePipeline] Completed | TraceId={TraceId} FileId={FileId} Chunks={ChunkCount} ParseMs={ParseMs} IndexMs={IndexMs} TotalMs={TotalMs}",
                traceId,
                doc.FileId,
                doc.Chunks?.Count ?? 0,
                parseStopwatch.ElapsedMilliseconds,
                indexStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Job Failed: {TraceId} Stage={Stage} DownloadMs={DownloadMs} ParseMs={ParseMs} IndexMs={IndexMs} TotalMs={TotalMs}",
                traceId,
                currentStage,
                downloadStopwatch.ElapsedMilliseconds,
                parseStopwatch.ElapsedMilliseconds,
                indexStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds);
            await NotifyStatusAsync(userId, traceId, ArtifactStatusConstants.BuildFailedStatus(ex.Message), null);

            if (Guid.TryParse(userId, out var failedUserId) && sessionId.HasValue)
            {
                await _clientNotifier.NotifyThinkingAsync(
                    failedUserId,
                    sessionId.Value,
                    string.Format(SmartDocumentConstants.ParsingThinking.FailedTemplate, ex.Message),
                    metadata: new Dictionary<string, object>
                    {
                        [SmartDocumentConstants.ParsingThinking.SourceMetadataKey] = SmartDocumentConstants.ParsingThinking.SourceName
                    });
            }

            throw;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }
    }

    private Task NotifyStatusAsync(string userId, string traceId, string status, SmartDocument? doc)
    {
        return _statusPublisher.PublishStatusAsync(userId, traceId, status, doc);
    }
}
