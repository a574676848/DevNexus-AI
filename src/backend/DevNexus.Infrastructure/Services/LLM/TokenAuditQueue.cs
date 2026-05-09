using System.Threading.Channels;
using DevNexus.Shared.DTOs;
using DevNexus.Infrastructure.Services.LLM;

namespace DevNexus.Infrastructure.Services.LLM;

public interface ITokenAuditQueue
{
    ValueTask QueueBackgroundWorkItemAsync(ModelInvocationAuditRecord workItem);
    ValueTask<ModelInvocationAuditRecord> DequeueAsync(CancellationToken cancellationToken);
}

public class TokenAuditQueue : ITokenAuditQueue
{
    private readonly Channel<ModelInvocationAuditRecord> _queue;

    public TokenAuditQueue()
    {
        // 容量限制为 5000，防止内存溢出
        // FullMode.Wait: 生产者在队列满时等待，形成背压，防止 OOM
        var options = new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // 只有一个后台服务读取
            SingleWriter = false // 多个请求线程写入
        };
        _queue = Channel.CreateBounded<ModelInvocationAuditRecord>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(ModelInvocationAuditRecord workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }

        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<ModelInvocationAuditRecord> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
