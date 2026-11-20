using Bpm.Compiler;
using Bpm.Runtime;
using Bpm.Testing;

namespace Shipping.Tests;

public class ShippingDefinitionTests
{
    [Fact]
    public void Carrier_booking_bound()
    {
        var p = ProcessCompiler.CompileOrThrow(CarrierBookingProcess.Definition);
        // 3 quotes x 15m + 2 backoffs x 1m + 30m cooling-off + 1h pickup window
        Assert.Equal(3 * TimeSpan.FromMinutes(15) + 2 * TimeSpan.FromMinutes(1) + TimeSpan.FromMinutes(30) + TimeSpan.FromHours(1), p.Analysis.TDag);
        Assert.Equal(
            ["Aborted(Killed)", "Completed(Dispatched)", "Failed(NoCarrierAvailable)", "Failed(PickupNotConfirmed)"],
            p.States.Values.Where(s => s.IsTerminal).Select(s => s.Id).Order());
    }

    [Fact]
    public void Delivery_tracking_bound()
    {
        var p = ProcessCompiler.CompileOrThrow(DeliveryTrackingProcess.Definition);
        Assert.Equal(TimeSpan.FromDays(15), p.Analysis.TDag);
        Assert.Equal(
            ["Aborted(Killed)", "Completed(Delivered)", "Completed(ReturnedToSender)", "Failed(CarrierUnresponsive)", "Failed(LostInTransit)"],
            p.States.Values.Where(s => s.IsTerminal).Select(s => s.Id).Order());
    }
}

public class ShippingScenarioTests
{
    private static (Simulation Sim, ProcessRegistry Registry) Run(CarrierBehaviour behaviour, int seed = 1)
    {
        var registry = new ProcessRegistry()
            .Register(CarrierBookingProcess.Definition)
            .Register(DeliveryTrackingProcess.Definition);
        var sim = new Simulation(registry, seed: seed).WithCarrier(behaviour);
        sim.Inject(new ShipmentRequested("s1", "Oslo"), "s1", TimeSpan.Zero);
        sim.Run();
        Assert.Empty(InvariantChecker.Check(sim.Store, registry).Violations);
        return (sim, registry);
    }

    [Fact]
    public void Booking_hands_off_to_delivery_tracking()
    {
        var (sim, _) = Run(CarrierBehaviour.Reliable);
        Assert.Equal("Dispatched", sim.Host.Load("CarrierBooking-s1")!.Outcome);
        var delivery = sim.Host.Load("DeliveryTracking-s1")!;
        Assert.Equal("Delivered", delivery.Outcome);
        Assert.Single(sim.ExternalEffects, e => e.Body is NotifyCustomerDelivered { SignedBy: "J. Doe" });
    }

    [Fact]
    public void Three_failed_deliveries_return_the_parcel()
    {
        var (sim, _) = Run(CarrierBehaviour.Reliable with { DeliveryFails = 1 });
        Assert.Equal("ReturnedToSender", sim.Host.Load("DeliveryTracking-s1")!.Outcome);
        Assert.Equal([1, 2, 3], sim.ExternalEffects.Select(e => e.Body).OfType<ScheduleDelivery>().Select(d => d.Attempt));
        Assert.Single(sim.ExternalEffects, e => e.Body is ReturnToSender);
    }

    [Fact]
    public void Silent_courier_opens_a_claim_after_the_attempt_window()
    {
        var (sim, _) = Run(CarrierBehaviour.Reliable with { SilentOnDelivery = 1 });
        var delivery = sim.Host.Load("DeliveryTracking-s1")!;
        Assert.Equal("CarrierUnresponsive", delivery.Outcome);
        Assert.Equal(TimeSpan.FromDays(2), delivery.EndedAt - delivery.StartedAt);
        Assert.Single(sim.ExternalEffects, e => e.Body is OpenCarrierClaim);
    }

    [Fact]
    public void Lost_pickup_confirmation_escalates_instead_of_dropping_the_shipment()
    {
        var registry = new ProcessRegistry().Register(CarrierBookingProcess.Definition).Register(DeliveryTrackingProcess.Definition);
        var sim = new Simulation(registry).WithCarrier();
        sim.Service<NotifyPickup>((_, _) => { }); // carrier never dispatches
        sim.Inject(new ShipmentRequested("s1", "Oslo"), "s1", TimeSpan.Zero);
        sim.Run();
        Assert.Equal("PickupNotConfirmed", sim.Host.Load("CarrierBooking-s1")!.Outcome);
        Assert.Single(sim.ExternalEffects, e => e.Body is EscalateToOps);
        Assert.Null(sim.Host.Load("DeliveryTracking-s1"));
    }

    [Fact]
    public void Carrier_refusing_everything_escalates_once_and_never_dispatches()
    {
        var (sim, _) = Run(CarrierBehaviour.Reliable with { Refuses = 1 });
        Assert.Equal("NoCarrierAvailable", sim.Host.Load("CarrierBooking-s1")!.Outcome);
        Assert.Equal(3, sim.ExternalEffects.Count(e => e.Body is RequestCarrier));
        Assert.Single(sim.ExternalEffects, e => e.Body is EscalateToOps { Refusals: 3 });
        Assert.Null(sim.Host.Load("DeliveryTracking-s1"));
    }
}

public class ShippingPropertyTests
{
    public static TheoryData<int> Seeds() => [.. Enumerable.Range(1, 10)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Compiled_machines_match_source_semantics(int seed)
    {
        var booking = DifferentialTester.Run(CarrierBookingProcess.Definition, new ShipmentRequested("c", "Oslo"),
            [_ => new CarrierBooked("c", "A"), _ => new CarrierUnavailable("c"), _ => new ShipmentDispatched("c", "A", "T"), _ => new Delivered("c", "x")], seed);
        Assert.True(booking.Passed, booking.ToString());

        var delivery = DifferentialTester.Run(DeliveryTrackingProcess.Definition, new ShipmentDispatched("c", "A", "T1"),
            [r => new Delivered("c", $"p{r.Next(2)}"), _ => new DeliveryAttemptFailed("c", "x"), _ => new ReturnedToSender("c"), _ => new CarrierBooked("c", "A")], seed);
        Assert.True(delivery.Passed, delivery.ToString());
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Chaos_preserves_invariants_across_the_handoff(int seed)
    {
        var registry = new ProcessRegistry()
            .Register(CarrierBookingProcess.Definition)
            .Register(DeliveryTrackingProcess.Definition);
        var sim = new Simulation(registry, FaultProfile.Chaos, seed).WithCarrier(new CarrierBehaviour
        {
            Refuses = 0.3, SilentOnBooking = 0.1, BookingMaxDelay = TimeSpan.FromMinutes(20),
            DeliveryFails = 0.4, SilentOnDelivery = 0.05, ReturnLost = 0.2,
        });
        var rnd = new Random(seed);
        for (var i = 0; i < 30; i++)
            sim.Inject(new ShipmentRequested($"s{i}", "Bergen"), $"s{i}", TimeSpan.FromMinutes(rnd.Next(0, 600)));
        sim.Run();

        var (violations, reports) = InvariantChecker.Check(sim.Store, registry);
        Assert.Empty(violations);
        Assert.Equal(30, reports.Count(r => r.Definition == "CarrierBooking"));
        foreach (var g in sim.ExternalEffects.GroupBy(e => e.CorrelationId))
        {
            Assert.True(g.Count(e => e.Body is RequestCarrier) <= 3);
            Assert.True(g.Count(e => e.Body is ScheduleDelivery) <= 3);
            Assert.True(g.Count(e => e.Body is EscalateToOps) <= 1);
            Assert.True(g.Count(e => e.Body is ReturnToSender) <= 1);
        }
        // The handoff is accounted for in both directions: a booking completes as Dispatched exactly
        // when a DeliveryTracking instance exists for the shipment.
        foreach (var b in reports.Where(r => r.Definition == "CarrierBooking"))
        {
            var tracking = sim.Host.Load(b.StreamId.Replace("CarrierBooking", "DeliveryTracking"));
            Assert.Equal(b.Outcome == "Completed(Dispatched)", tracking is not null);
        }
    }
}
