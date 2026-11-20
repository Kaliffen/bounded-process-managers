using Bpm.Compiler;
using Bpm.Dsl;
using static Bpm.Dsl.Flow<Bpm.Tests.KState>;

namespace Bpm.Tests;

public class RejectionTests
{
    private static ProcessDefinition Def(Step body, TimeSpan? budget = null) => new()
    {
        Name = "T",
        StateType = typeof(KState),
        MaxBudget = budget,
        StartMessageType = typeof(KStart),
        Init = _ => new KState(),
        Body = body,
    };

    private static JoinStep JoinAll(TimeSpan? timeout, params JoinArm[] arms) => new()
    {
        JoinKind = JoinKind.All,
        Arms = [.. arms],
        Timeout = timeout,
        OnMatched = Complete("ok"),
        OnTimeout = Fail("timeout"),
    };

    private static void AssertRejected(ProcessDefinition def, string code, CompilerOptions? options = null)
    {
        var r = ProcessCompiler.Compile(def, options);
        Assert.False(r.Success, $"expected {code} but compiled");
        Assert.Contains(r.Errors, d => d.Code == code);
    }

    [Fact]
    public void Join_without_timeout_bypassing_builder_is_rejected()
        => AssertRejected(Def(JoinAll(null, On<Ack>())), Codes.UnboundedJoin);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_timeout_is_rejected(int seconds)
        => AssertRejected(Def(JoinAll(TimeSpan.FromSeconds(seconds), On<Ack>())), Codes.InvalidTimeout);

    [Fact]
    public void Infinite_timeout_is_rejected()
        => AssertRejected(Def(JoinAll(TimeSpan.MaxValue, On<Ack>())), Codes.InvalidTimeout);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Retry_bound_must_be_static_and_finite(int attempts)
        => AssertRejected(Def(Retry(attempts, TimeSpan.Zero, AttemptFailed()).OnExhausted(Fail("x"))), Codes.InvalidRetryBound);

    [Fact]
    public void Cycle_built_by_mutating_the_ast_is_rejected()
    {
        var join = JoinAll(TimeSpan.FromMinutes(1), On<Ack>());
        var publish = new PublishStep { Commands = _ => [new Cmd("loop")], Label = "loop", Next = join };
        join.OnTimeout = publish; // unbounded loop: timeout -> publish -> join -> ...
        AssertRejected(Def(publish), Codes.Cycle);
    }

    [Fact]
    public void Missing_continuation_is_rejected()
        => AssertRejected(Def(new BranchStep { Predicate = _ => true, Label = "p", Then = Complete("a"), Else = null }), Codes.MissingContinuation);

    [Fact]
    public void Missing_start_message_is_rejected()
        => AssertRejected(new ProcessDefinition { Name = "T", StateType = typeof(KState), Body = Complete("x") }, Codes.MissingStart);

    [Fact]
    public void Duplicate_names_are_rejected()
    {
        var inner = Join.All(On<Ack>()).Named("Same").WithTimeout(1.Minutes()).OnMatched(Complete("a")).OnTimeout(Fail("b"));
        var outer = Join.All(On<Ping>()).Named("Same").WithTimeout(1.Minutes()).OnMatched(inner).OnTimeout(Fail("c"));
        AssertRejected(Def(outer), Codes.DuplicateName);
    }

    [Fact]
    public void Budget_exceeded_is_rejected()
    {
        var a = Join.All(On<Ack>()).WithTimeout(6.Minutes()).OnMatched(Complete("ok")).OnTimeout(Fail("t"));
        var b = Join.All(On<Ping>()).WithTimeout(6.Minutes()).OnMatched(a).OnTimeout(Fail("t2"));
        var r = ProcessCompiler.Compile(Def(b, budget: 10.Minutes()));
        var d = Assert.Single(r.Errors);
        Assert.Equal(Codes.BudgetExceeded, d.Code);
        Assert.Contains("12m", d.Message);
    }

    [Fact]
    public void State_explosion_is_capped()
        => AssertRejected(Def(KitchenSink.Create().Body!), Codes.StateLimit, new CompilerOptions { MaxStates = 10 });

    [Fact]
    public void Retry_that_cannot_fit_in_horizon_warns()
    {
        var join = Join.All(Retry(3, 1.Minutes(), On<Pong>(), On<Nack>(), _ => new Cmd("again")))
            .WithTimeout(90.Seconds()).OnMatched(Complete("ok")).OnTimeout(Fail("t"));
        var r = ProcessCompiler.Compile(Def(join));
        Assert.True(r.Success);
        Assert.Contains(r.Diagnostics, d => d.Code == Codes.RetryUnreachable && d.Severity == Severity.Warning);
    }

    [Fact]
    public void Empty_join_is_rejected() => AssertRejected(Def(JoinAll(TimeSpan.FromMinutes(1))), Codes.EmptyJoin);

    [Fact]
    public void Ambiguous_arms_are_rejected() => AssertRejected(Def(JoinAll(TimeSpan.FromMinutes(1), On<Ack>("a"), On<Ack>("b"))), Codes.AmbiguousArm);

    [Fact]
    public void Retry_arm_in_disjunctive_join_is_rejected()
    {
        var j = new JoinStep
        {
            JoinKind = JoinKind.Any,
            Timeout = TimeSpan.FromMinutes(1),
            Arms = [Retry(2, TimeSpan.Zero, On<Pong>(), On<Nack>(), _ => new Cmd("x"))],
            OnTimeout = Fail("t"),
        };
        AssertRejected(Def(j), Codes.RetryInAny);
    }

    [Fact]
    public void AttemptFailed_outside_retry_is_rejected() => AssertRejected(Def(AttemptFailed()), Codes.AttemptFailedOutsideRetry);

    [Fact]
    public void Lambdas_against_another_state_type_are_rejected()
        => AssertRejected(Def(Flow<string>.Publish(_ => new Cmd("x")).Then(Complete("ok"))), Codes.StateTypeMismatch);

    [Fact]
    public void All_errors_are_reported_at_once()
    {
        var r = ProcessCompiler.Compile(Def(new BranchStep
        {
            Predicate = _ => true,
            Label = "p",
            Then = JoinAll(null, On<Ack>()),
            Else = AttemptFailed(),
        }));
        Assert.Contains(r.Errors, d => d.Code == Codes.UnboundedJoin);
        Assert.Contains(r.Errors, d => d.Code == Codes.AttemptFailedOutsideRetry);
    }

    [Fact]
    public void Diagnostics_point_at_the_source_line()
    {
        var r = ProcessCompiler.Compile(Def(AttemptFailed()));
        var d = Assert.Single(r.Errors);
        Assert.EndsWith("CompilerTests.cs", d.Location!.File);
        Assert.Matches(@"CompilerTests\.cs\(\d+\): error BPM014", d.ToString());
    }
}

public class ScalingTests
{
    /// <summary>Paper §8.1: joins of two to eight inputs; a k-way conjunction has 2^k - 1 partial states.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void K_way_join_state_count(int k)
    {
        Type[] types = [typeof(Ack), typeof(Nack), typeof(Ping), typeof(Pong), typeof(Done), typeof(KStart), typeof(Cmd), typeof(string)];
        var arms = types.Take(k).Select(t => (JoinArm)new MessageArm { MessageType = t, Name = t.Name }).ToArray();
        var def = new ProcessDefinition
        {
            Name = "K",
            StateType = typeof(KState),
            StartMessageType = typeof(KState),
            Init = _ => new KState(),
            Body = Join.All(arms).WithTimeout(1.Minutes()).OnMatched(Complete("ok")).OnTimeout(Fail("t")),
        };
        var p = ProcessCompiler.CompileOrThrow(def);
        var waitStates = p.States.Values.Count(s => s.Kind == StateKind.Wait);
        Assert.Equal((1 << k) - 1, waitStates);
        Assert.Equal(TimeSpan.FromMinutes(1), p.Analysis.TDag);
    }

    [Fact]
    public void Kitchen_sink_compiles_and_is_bounded()
    {
        var p = ProcessCompiler.CompileOrThrow(KitchenSink.Create());
        // Outer attempt 1 (1m) + backoff (10s) + attempt 2 (1m) + Final (30s); the 5s delay is on the
        // matched path, which ends sooner.
        Assert.Equal(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(40), p.Analysis.TDag);
    }
}
