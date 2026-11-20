using Bpm.Dsl;
using Bpm.Runtime;
using static Bpm.Dsl.Flow<Bpm.Tests.KState>;

namespace Bpm.Tests;

/// <summary>The real-time, in-process host: wall clock, background timer loop, local command adapters.</summary>
public class LocalRuntimeTests
{
    private static ProcessDefinition PingPong(TimeSpan horizon) =>
        ProcessManager.Define<KState>("PingPong")
            .StartedBy<KStart>(_ => new KState())
            .StartWith(_ => [new Cmd("ping")])
            .Then(Join.All(On<Pong>()).WithTimeout(horizon)
                .OnMatched(Complete("Ponged"))
                .OnTimeout(Fail("NoPong")));

    [Fact]
    public async Task Completes_when_the_local_adapter_replies()
    {
        var registry = new ProcessRegistry().Register(PingPong(TimeSpan.FromSeconds(5)));
        await using var runtime = new LocalRuntime(registry, new InMemoryEventStore())
            .On<Cmd>(async (cmd, ctx) =>
            {
                await Task.Delay(50);
                await ctx.Reply(new Pong("p"));
            })
            .Start();

        await runtime.Send(new KStart("a"), "a");
        var state = await runtime.WaitForTerminal("PingPong", "a", TimeSpan.FromSeconds(5));
        Assert.Equal("Ponged", state.Outcome);
    }

    [Fact]
    public async Task Real_timer_fires_when_nobody_replies()
    {
        var registry = new ProcessRegistry().Register(PingPong(TimeSpan.FromMilliseconds(300)));
        await using var runtime = new LocalRuntime(registry, new InMemoryEventStore()).Start();

        await runtime.Send(new KStart("b"), "b");
        var state = await runtime.WaitForTerminal("PingPong", "b", TimeSpan.FromSeconds(5));
        Assert.Equal("NoPong", state.Outcome);
        Assert.InRange((state.EndedAt - state.StartedAt)!.Value, TimeSpan.FromMilliseconds(300), state.LifetimeBound);
    }

    [Fact]
    public async Task Restart_resumes_pending_timers_from_the_durable_store()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bpm-local-" + Guid.NewGuid().ToString("N"));
        try
        {
            var registry = new ProcessRegistry().Register(PingPong(TimeSpan.FromMilliseconds(500)));
            var checkpoint = Path.Combine(dir, "outbox.checkpoint");
            await using (var first = new LocalRuntime(registry, new FileEventStore(dir), checkpointFile: checkpoint).Start())
            {
                await first.Send(new KStart("c"), "c");
                await Task.Delay(100); // started, timer pending — then the process "dies"
            }

            await using var second = new LocalRuntime(registry, new FileEventStore(dir), checkpointFile: checkpoint).Start();
            var state = await second.WaitForTerminal("PingPong", "c", TimeSpan.FromSeconds(5));
            Assert.Equal("NoPong", state.Outcome);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
