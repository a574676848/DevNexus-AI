using System.Collections.Concurrent;
using DevNexus.Shared.DTOs.Swarm;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 智能体状态仓储，作为 Hub 重连补偿的单一状态来源。
/// </summary>
public interface ISwarmAgentStatusStore
{
    void Upsert(string sessionId, string agentName, string status, string currentAction);

    IReadOnlyList<AgentStatusDto> GetSnapshot(string sessionId);

    void Clear(string sessionId);
}

/// <inheritdoc />
public sealed class SwarmAgentStatusStore : ISwarmAgentStatusStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, AgentStatusDto>> _sessions = new();

    public void Upsert(string sessionId, string agentName, string status, string currentAction)
    {
        var agents = _sessions.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, AgentStatusDto>());
        agents[agentName] = new AgentStatusDto
        {
            Name = agentName,
            Status = status,
            CurrentAction = currentAction
        };
    }

    public IReadOnlyList<AgentStatusDto> GetSnapshot(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var agents)
            ? agents.Values
                .Select(agent => new AgentStatusDto
                {
                    Name = agent.Name,
                    Status = agent.Status,
                    CurrentAction = agent.CurrentAction
                })
                .OrderBy(agent => agent.Name, StringComparer.Ordinal)
                .ToList()
            : [];
    }

    public void Clear(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
