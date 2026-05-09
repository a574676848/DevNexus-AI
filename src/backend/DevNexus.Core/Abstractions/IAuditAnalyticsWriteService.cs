using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

public interface IAuditAnalyticsWriteService
{
    Task RecordUsageAsync(
        ModelInvocationAuditRecord record,
        CancellationToken cancellationToken = default);
}