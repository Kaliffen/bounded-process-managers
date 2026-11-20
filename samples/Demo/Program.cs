using System.Globalization;
using Bpm.Compiler;
using Bpm.Dsl;
using Bpm.Runtime;
using Bpm.Testing;
using Demo;
using OrderFulfillment;
using Shipping;

// Local demo host composing two bounded contexts (orders, shipping) on fake infrastructure.
//   dotnet run --project samples/Demo -- <scenario> [--seed N] [--store DIR]
// Simulated (virtual clock): happy retry refund kill shipping pipeline chaos drain
// Real time (wall clock, LocalRuntime): live

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var scenario = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "all";
var seed = int.TryParse(Opt("--seed"), out var s) ? s : 7;
var storeDir = Opt("--store");

var orderV1 = ProcessCompiler.CompileOrThrow(OrderProcess.Definition);
var orderV2 = ProcessCompiler.CompileOrThrow(OrderProcessV2.Definition);
var booking = ProcessCompiler.CompileOrThrow(CarrierBookingProcess.Definition);
var delivery = ProcessCompiler.CompileOrThrow(DeliveryTrackingProcess.Definition);

var scenarios = new Dictionary<string, Func<Task>>
{
    ["happy"] = () => Order("Order happy path: both replies arrive", WorldBehaviour.Reliable),
    ["retry"] = () => Order("Order: payment declined once, retried after 30s backoff", WorldBehaviour.Reliable, declineFirst: true),
    ["refund"] = () => Order("Order: inventory never replies; payment captured; timeout triggers explicit refund",
        WorldBehaviour.Reliable with { InventoryNeverReplies = 1 }),
    ["kill"] = () => Order("Order: operator kills the instance mid-flight", WorldBehaviour.Reliable with { InventoryNeverReplies = 1 }, killAt: 2.Minutes()),
    ["shipping"] = ShippingScenario,
    ["pipeline"] = Pipeline,
    ["chaos"] = Chaos,
    ["drain"] = Drain,
    ["live"] = Live,
};

if (scenario == "all")
    foreach (var run in scenarios.Values) await run();
else if (scenarios.TryGetValue(scenario, out var one)) await one();
else Console.Error.WriteLine($"unknown scenario '{scenario}'; choose one of: {string.Join(", ", scenarios.Keys)}, all");
return;

Task Order(string title, WorldBehaviour behaviour, bool declineFirst = false, TimeSpan? killAt = null)
{
    Header(title);
    var registry = new ProcessRegistry().Register(orderV1);
    var sim = NewSim(registry, FaultProfile.None, behaviour);
    sim.Verbose = true;
    if (declineFirst)
    {
        var charged = 0;
        sim.Service<ChargePayment>((cmd, ctx) =>
            ctx.Reply(++charged == 1
                ? new PaymentDeclined(cmd.OrderId, "insufficient funds")
                : new PaymentCaptured(cmd.OrderId, "pay-42"), 3.Seconds()));
    }
    sim.Inject(new OrderSubmitted("order-1", [new OrderItem("sku-1", 2)], 99.95m), "order-1", TimeSpan.Zero);
    if (killAt is { } k) sim.Inject(new KillProcess("operator request"), "order-1", k);
    sim.Run();
    Summarize(sim, registry);
    return Task.CompletedTask;
}

Task ShippingScenario()
{
    Header("Shipping: carrier refuses twice, books, dispatches; first delivery attempt fails, second succeeds");
    var registry = new ProcessRegistry().Register(booking).Register(delivery);
    var sim = NewSim(registry, FaultProfile.None, WorldBehaviour.Reliable);
    sim.Verbose = true;
    var asked = 0;
    sim.Service<RequestCarrier>((cmd, ctx) =>
        ctx.Reply(++asked < 3 ? new CarrierUnavailable(cmd.ShipmentId) : new CarrierBooked(cmd.ShipmentId, "ACME Freight"), 2.Minutes()));
    var attempts = 0;
    sim.Service<ScheduleDelivery>((cmd, ctx) =>
        ctx.Reply(++attempts == 1 ? new DeliveryAttemptFailed(cmd.ShipmentId, "nobody home") : new Delivered(cmd.ShipmentId, "J. Doe"), 6.Hours()));
    sim.Inject(new ShipmentRequested("ship-1", "Oslo"), "ship-1", TimeSpan.Zero);
    sim.Run();
    Summarize(sim, registry);
    return Task.CompletedTask;
}

Task Pipeline()
{
    Header("Pipeline: order -> (integration) -> carrier booking -> (handoff) -> delivery tracking");
    var registry = new ProcessRegistry().Register(orderV1).Register(booking).Register(delivery);
    var sim = NewSim(registry, FaultProfile.None, WorldBehaviour.Reliable).WithOrderToShippingIntegration();
    sim.Verbose = true;
    sim.Inject(new OrderSubmitted("order-7", [new OrderItem("sku-9", 1)], 15m), "order-7", TimeSpan.Zero);
    sim.Run();
    Summarize(sim, registry);
    return Task.CompletedTask;
}

Task Chaos()
{
    Header($"Chaos: 300 orders flowing into shipping, every fault injected (seed {seed})");
    var registry = new ProcessRegistry().Register(orderV1).Register(booking).Register(delivery);
    var behaviour = new WorldBehaviour
    {
        InventoryNeverReplies = 0.1, PaymentDeclines = 0.3, PaymentNeverReplies = 0.05,
        InventoryMaxDelay = 4.Minutes(), PaymentMaxDelay = 2.Minutes(),
        CarrierRefuses = 0.4, CarrierSilent = 0.1, CarrierMaxDelay = 20.Minutes(), DeliveryFails = 0.35,
    };
    var sim = NewSim(registry, FaultProfile.Chaos, behaviour).WithOrderToShippingIntegration();
    for (var i = 0; i < 300; i++)
    {
        var at = TimeSpan.FromSeconds(sim.Random.Next(0, 3600));
        sim.Inject(new OrderSubmitted($"o{i}", [new OrderItem("sku", 1)], 10m + i), $"o{i}", at);
        if (i % 15 == 0) sim.Inject(new KillProcess("ops"), $"o{i}", at + TimeSpan.FromSeconds(sim.Random.Next(1, 360)));
    }
    sim.Run();

    var (violations, reports) = InvariantChecker.Check(sim.Store, registry);
    Console.WriteLine($"injected: {sim.Store.FailedAppends} failed appends, {sim.DispatcherCrashes} dispatcher crashes, {sim.HostRestarts} host restarts, " +
                      $"{sim.DropsInjected} drops, {sim.DuplicatesInjected} duplicates");
    Console.WriteLine();
    Console.WriteLine($"{"definition",-18} {"outcome",-40} {"count",5} {"max observed",16} {"bound",12}");
    foreach (var g in reports.GroupBy(r => (r.Definition, r.Outcome)).OrderBy(g => g.Key.Definition).ThenBy(g => g.Key.Outcome))
        Console.WriteLine($"{g.Key.Definition,-18} {g.Key.Outcome,-40} {g.Count(),5} {g.Max(r => r.Lifetime ?? TimeSpan.Zero).Pretty(),16} {g.First().Bound.Pretty(),12}");
    Console.WriteLine();
    Console.WriteLine(violations.Count == 0 ? "INVARIANTS: all hold" : "INVARIANT VIOLATIONS:\n  " + string.Join("\n  ", violations));
    return Task.CompletedTask;
}

Task Drain()
{
    Header("Versioning: v1 instances drain under their pinned table after v2 is deployed");
    var registry = new ProcessRegistry().Register(orderV1);
    var sim = NewSim(registry, FaultProfile.None, WorldBehaviour.Reliable with { InventoryNeverReplies = 0.5, PaymentDeclines = 0.3 });
    for (var i = 0; i < 20; i++)
        sim.Inject(new OrderSubmitted($"a{i}", [], 1m), $"a{i}", TimeSpan.FromSeconds(i * 5));
    sim.Run(2.Minutes());
    registry.Register(orderV2);
    Console.WriteLine($"{sim.Clock.Now:HH:mm:ss} deployed v2 (table {orderV2.TableHash}); v1 table {orderV1.TableHash} stays registered for pinned instances");
    foreach (var d in DrainPlanner.Plan(sim.Host, registry))
        Console.WriteLine($"  {d.Definition} v{d.Version}: {d.ActiveInstances} active, guaranteed drained by {d.DrainedBy:HH:mm:ss} (start + T_max)");
    for (var i = 0; i < 10; i++)
        sim.Inject(new OrderSubmitted($"b{i}", [], 1m), $"b{i}", TimeSpan.FromSeconds(i));
    sim.Run();
    var (violations, reports) = InvariantChecker.Check(sim.Store, registry);
    foreach (var g in reports.GroupBy(r => r.Version))
        Console.WriteLine($"  v{g.Key}: {g.Count()} instances, longest lifetime {g.Max(r => r.Lifetime)!.Value.Pretty()} (bound {g.First().Bound.Pretty()})");
    Console.WriteLine(violations.Count == 0 ? "INVARIANTS: all hold" : string.Join("\n", violations));
    return Task.CompletedTask;
}

async Task Live()
{
    Header("Live: real wall clock, in-process LocalRuntime, local service adapters (~1s)");
    var registry = new ProcessRegistry().Register(orderV1);
    IEventStore store = storeDir is null ? new InMemoryEventStore() : new FileEventStore(storeDir);
    var started = DateTimeOffset.UtcNow;
    string T() => $"+{(DateTimeOffset.UtcNow - started).TotalMilliseconds,6:F0}ms";

    await using var runtime = new LocalRuntime(registry, store, checkpointFile: storeDir is null ? null : Path.Combine(storeDir, "outbox.checkpoint"))
        .On<ReserveInventory>(async (cmd, ctx) => { await Task.Delay(150); await ctx.Reply(new InventoryReserved(cmd.OrderId)); })
        .On<ChargePayment>(async (cmd, ctx) => { await Task.Delay(300); await ctx.Reply(new PaymentCaptured(cmd.OrderId, "pay-live")); });
    runtime.Handled += r => Console.WriteLine($"{T()} {r}");
    runtime.Ended += e => Console.WriteLine($"{T()} END {e.StreamId}: {e.Terminal}(\"{e.Outcome}\")");
    runtime.Faulted += (env, ex) => Console.WriteLine($"{T()} FAULT {env}: {ex.Message}");
    runtime.Start();

    await runtime.Send(new OrderSubmitted("live-1", [new OrderItem("sku", 1)], 5m), "live-1");
    await runtime.Send(new OrderSubmitted("live-2", [new OrderItem("sku", 1)], 5m), "live-2");
    await Task.Delay(100);
    await runtime.Send(new KillProcess("operator"), "live-2");

    foreach (var id in new[] { "live-1", "live-2" })
    {
        var st = await runtime.WaitForTerminal("OrderFulfillment", id, TimeSpan.FromSeconds(10));
        Console.WriteLine($"=> {st.StreamId}: {st.Terminal}({st.Outcome}) after {(st.EndedAt - st.StartedAt)!.Value.Pretty()}");
    }
}

Simulation NewSim(ProcessRegistry registry, FaultProfile faults, WorldBehaviour behaviour)
{
    IEventStore? store = storeDir is null ? null : new FileEventStore(Path.Combine(storeDir, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}"));
    return new Simulation(registry, faults, seed, store).WithFakeWorld(behaviour);
}

void Summarize(Simulation sim, ProcessRegistry registry)
{
    var (violations, reports) = InvariantChecker.Check(sim.Store, registry);
    foreach (var r in reports)
        Console.WriteLine($"=> {r.StreamId}: {r.Outcome} after {r.Lifetime?.Pretty()} (compiler bound T_max = {r.Bound.Pretty()})");
    Console.WriteLine(violations.Count == 0 ? "INVARIANTS: all hold (replay verified)" : string.Join("\n", violations));
}

void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 100));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 100));
}

string? Opt(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
