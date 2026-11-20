using Bpm.Compiler;
using Bpm.Generation;

namespace OrderFulfillment.Tests;

/// <summary>
/// Conformance of the compiled order process with the paper's §3.4 listing, §4.1 transition table and
/// §5.1 bound.
/// </summary>
public class PaperConformanceTests
{
    private static readonly CompiledProcess P = ProcessCompiler.CompileOrThrow(OrderProcess.Definition);

    private static Transition Row(string from, string input, string? guard = null)
        => P.Transitions.Single(t => t.From == from && t.Input.Display == input
                                     && (guard is null || t.GuardDisplay == guard));

    [Fact]
    public void Lifetime_bound_matches_section_5_1()
    {
        // "the 30-second payment retry executes inside the 5-minute join horizon, yielding T_DAG = 5 minutes"
        Assert.Equal(TimeSpan.FromMinutes(5), P.Analysis.TDag);
        Assert.True(P.Analysis.TDag <= OrderProcess.Definition.MaxBudget);
        Assert.Equal(P.Analysis.TDag + P.Analysis.MaxTimerDeliveries * TimeSpan.FromSeconds(1) + P.Analysis.MaxProcessingSteps * TimeSpan.FromSeconds(1), P.Analysis.TMax);
    }

    [Fact]
    public void Init_row_publishes_both_commands_and_binds_the_join_horizon()
    {
        var init = Row("Init", "OrderSubmitted");
        Assert.Equal("InvPay[Inv:wait, Pay:try1]", init.To);
        Assert.Equal(["Publish ReserveInventory, ChargePayment", "ScheduleTimer(InvPay.T, 5m)"], init.Effects.Select(e => e.Display));
    }

    [Fact]
    public void Partial_arrivals_never_restart_the_horizon()
    {
        // §3.3: the timeout belongs to the join's waiting horizon and must not be restarted.
        var horizonSchedules = P.Transitions.Where(t => t.Effects.Any(e => e.Kind == EffectKind.ScheduleTimer && e.Ref == "InvPay.T"));
        Assert.Equal([0], horizonSchedules.Select(t => t.Index));

        Assert.Empty(Row("InvPay[Inv:wait, Pay:try1]", "InventoryReserved").Effects);
        Assert.Empty(Row("InvPay[Inv:wait, Pay:try1]", "PaymentCaptured").Effects);
    }

    [Fact]
    public void Decline_schedules_backoff_then_reissues_charge()
    {
        var decline = Row("InvPay[Inv:wait, Pay:try1]", "PaymentDeclined");
        Assert.Equal("InvPay[Inv:wait, Pay:backoff1]", decline.To);
        Assert.Equal(["ScheduleTimer(InvPay.Pay.B, 30s)"], decline.Effects.Select(e => e.Display));

        var backoff = Row("InvPay[Inv:wait, Pay:backoff1]", "TimerFired(InvPay.Pay.B)");
        Assert.Equal("InvPay[Inv:wait, Pay:try2]", backoff.To);
        Assert.Equal(["Publish ChargePayment"], backoff.Effects.Select(e => e.Display));
    }

    [Fact]
    public void Timeout_after_payment_compensates_explicitly()
    {
        var t = Row("InvPay[Inv:wait, Pay:got]", "TimerFired(InvPay.T)", "HasPayment && !HasInventory");
        Assert.Equal("Failed(TimeoutAfterPayment_Refunded)", t.To);
        Assert.Equal(["Publish RefundPayment", "Failed(\"TimeoutAfterPayment_Refunded\")"], t.Effects.Select(e => e.Display));
    }

    [Fact]
    public void Completion_cancels_the_horizon()
    {
        var done = Row("InvPay[Inv:wait, Pay:got]", "InventoryReserved");
        Assert.Equal("Completed(Fulfilled)", done.To);
        Assert.Contains(done.Effects, e => e.Display == "CancelTimer(InvPay.T)");
    }

    [Fact]
    public void Exhausted_retry_resolves_the_join_early()
    {
        var last = Row("InvPay[Inv:got, Pay:try2]", "PaymentDeclined", "!(HasPayment && !HasInventory)");
        Assert.Equal("Failed(TimeoutBeforePayment)", last.To);
        Assert.Contains(last.Effects, e => e.Display == "CancelTimer(InvPay.T)");
    }

    [Fact]
    public void Every_wait_state_accepts_kill()
    {
        foreach (var s in P.States.Values.Where(s => s.Kind == StateKind.Wait))
            Assert.Equal("Aborted(Killed)", Row(s.Id, "Kill").To);
    }

    [Fact]
    public void Compilation_is_deterministic()
    {
        var again = ProcessCompiler.CompileOrThrow(OrderProcess.Definition);
        Assert.Equal(P.TableHash, again.TableHash);
        Assert.Equal(Generators.All(P), Generators.All(again));
    }

    [Fact]
    public void Generated_spec_has_one_scenario_per_path()
    {
        var spec = Generators.Specifications(P);
        Assert.Contains("**When** `OrderSubmitted` is received", spec);
        Assert.Contains("ends Failed \"TimeoutAfterPayment_Refunded\"", spec);
        Assert.Contains("ends Completed \"Fulfilled\"", spec);
    }
}
