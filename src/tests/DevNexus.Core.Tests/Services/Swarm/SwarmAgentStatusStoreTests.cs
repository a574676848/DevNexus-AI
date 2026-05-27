using DevNexus.Core.Services.Swarm;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

public class SwarmAgentStatusStoreTests
{
    [Fact]
    public void Upsert_Should_Return_Latest_Agent_Snapshot()
    {
        var store = new SwarmAgentStatusStore();
        const string sessionId = "session-1";

        store.Upsert(sessionId, "Reviewer", "评估中", "检查输出");
        store.Upsert(sessionId, "Reviewer", "通过", "质量达标");

        var snapshot = store.GetSnapshot(sessionId);

        snapshot.Should().ContainSingle();
        snapshot[0].Name.Should().Be("Reviewer");
        snapshot[0].Status.Should().Be("通过");
        snapshot[0].CurrentAction.Should().Be("质量达标");
    }

    [Fact]
    public void Clear_Should_Remove_Session_Agents()
    {
        var store = new SwarmAgentStatusStore();
        const string sessionId = "session-1";

        store.Upsert(sessionId, "Planner", "执行中", "拆解任务");
        store.Clear(sessionId);

        store.GetSnapshot(sessionId).Should().BeEmpty();
    }

    [Fact]
    public void GetSnapshot_Should_Return_Copy()
    {
        var store = new SwarmAgentStatusStore();
        const string sessionId = "session-1";

        store.Upsert(sessionId, "Planner", "执行中", "拆解任务");
        var snapshot = store.GetSnapshot(sessionId);
        var changed = snapshot[0] with { Status = "外部修改" };

        changed.Status.Should().Be("外部修改");
        store.GetSnapshot(sessionId)[0].Status.Should().Be("执行中");
    }
}
