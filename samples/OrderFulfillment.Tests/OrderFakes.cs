using Bpm.Testing;

namespace OrderFulfillment.Tests;

public sealed record OrderServicesBehaviour
{
    public double InventoryNeverReplies { get; init; }
    public TimeSpan InventoryMaxDelay { get; init; } = TimeSpan.FromSeconds(20);
    public double PaymentDeclines { get; init; }
    public double PaymentNeverReplies { get; init; }
    public TimeSpan PaymentMaxDelay { get; init; } = TimeSpan.FromSeconds(20);

    public static OrderServicesBehaviour Reliable { get; } = new();
}

/// <summary>The external world of the order context: inventory and payment services.</summary>
public static class OrderFakes
{
    public static Simulation WithOrderServices(this Simulation sim, OrderServicesBehaviour? behaviour = null)
    {
        var b = behaviour ?? OrderServicesBehaviour.Reliable;
        sim.Service<ReserveInventory>((cmd, ctx) =>
        {
            if (ctx.Random.NextDouble() < b.InventoryNeverReplies) return;
            ctx.Reply(new InventoryReserved(cmd.OrderId), Delay(ctx, b.InventoryMaxDelay));
        });
        sim.Service<ChargePayment>((cmd, ctx) =>
        {
            if (ctx.Random.NextDouble() < b.PaymentNeverReplies) return;
            ctx.Reply(ctx.Random.NextDouble() < b.PaymentDeclines
                ? new PaymentDeclined(cmd.OrderId, "insufficient funds")
                : new PaymentCaptured(cmd.OrderId, $"pay-{ctx.Command.MessageId[..8]}"), Delay(ctx, b.PaymentMaxDelay));
        });
        sim.Service<RefundPayment>((_, _) => { });
        return sim;
    }

    private static TimeSpan Delay(ServiceContext ctx, TimeSpan max) => TimeSpan.FromTicks((long)(ctx.Random.NextDouble() * max.Ticks));
}
