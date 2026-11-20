using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using Bpm.Compiler;
using Bpm.Dsl;

namespace Bpm.Runtime;

/// <summary>What a local command handler (an adapter to a domain service) gets.</summary>
public sealed class CommandContext(LocalRuntime runtime, Envelope command)
{
    private int _replies;

    public Envelope Command { get; } = command;

    /// <summary>
    /// Sends a reply into the process inbox. Reply ids derive from the command id, so a re-dispatched
    /// command produces the same reply ids and the instance deduplicates them.
    /// </summary>
    public ValueTask Reply(object message)
        => runtime.Deliver(new Envelope(DeterministicId.For($"{Command.MessageId}:reply:{_replies++}"), Command.CorrelationId, message, Command.MessageId));
}

/// <summary>
/// Real-time, single-process host: one inbox consumer (process host + outbox), a timer loop on the
/// wall clock, and in-process command handlers. Restart-safe when given a durable store and a
/// checkpoint file: the timer schedule is rebuilt from the streams and the outbox resumes from its
/// checkpoint (anything after it is re-dispatched with the same ids).
/// </summary>
public sealed class LocalRuntime : IMessageBus, IAsyncDisposable
{
    private readonly Channel<Envelope> _inbox = Channel.CreateUnbounded<Envelope>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<Type, Func<object, CommandContext, Task>> _handlers = [];
    private readonly ConcurrentDictionary<string, byte> _executedCommands = new();
    private readonly ProcessRegistry _registry;
    private readonly IEventStore _store;
    private readonly IClock _clock;
    private readonly string? _checkpointFile;
    private readonly CancellationTokenSource _stop = new();
    private Task? _inboxLoop;
    private Task? _timerLoop;

    public LocalRuntime(ProcessRegistry registry, IEventStore store, IClock? clock = null, string? checkpointFile = null)
    {
        _registry = registry;
        _store = store;
        _clock = clock ?? new SystemClock();
        _checkpointFile = checkpointFile;
        Timers = new TimerService();
        Host = new ProcessHost(registry, store, _clock);
        Outbox = new OutboxDispatcher(store, this, Timers);
    }

    public ProcessHost Host { get; }
    public TimerService Timers { get; }
    public OutboxDispatcher Outbox { get; }
    public TimeSpan TimerResolution { get; init; } = TimeSpan.FromMilliseconds(20);

    public event Action<HandleResult>? Handled;
    public event Action<ProcessEnded>? Ended;
    public event Action<Envelope, Exception>? Faulted;

    /// <summary>Registers the local adapter for a command type.</summary>
    public LocalRuntime On<TCommand>(Func<TCommand, CommandContext, Task> handler)
    {
        _handlers[typeof(TCommand)] = (c, ctx) => handler((TCommand)c, ctx);
        return this;
    }

    public LocalRuntime On<TCommand>(Action<TCommand, CommandContext> handler)
        => On<TCommand>((c, ctx) => { handler(c, ctx); return Task.CompletedTask; });

    public LocalRuntime Start()
    {
        if (_checkpointFile is not null && File.Exists(_checkpointFile))
            Outbox.ResumeFrom(long.Parse(File.ReadAllText(_checkpointFile), CultureInfo.InvariantCulture));
        Timers.Rebuild(Host.Instances());
        Pump();
        _inboxLoop = Task.Run(InboxLoop);
        _timerLoop = Task.Run(TimerLoop);
        return this;
    }

    /// <summary>Sends an external input (start message, reply, Kill) to the process inbox.</summary>
    public ValueTask Send(object message, string correlationId, string? messageId = null)
        => Deliver(new Envelope(messageId ?? Guid.NewGuid().ToString(), correlationId, message));

    internal ValueTask Deliver(Envelope envelope) => _inbox.Writer.WriteAsync(envelope);

    /// <summary>Waits until an instance is terminal (convenience for tests and demos).</summary>
    public async Task<InstanceState> WaitForTerminal(string definition, string correlationId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var stream = Engine.StreamIdFor(definition, correlationId);
        while (DateTime.UtcNow < deadline)
        {
            if (Host.Load(stream) is { IsTerminal: true } s) return s;
            await Task.Delay(10);
        }
        throw new TimeoutException($"{stream} did not reach a terminal outcome within {timeout}");
    }

    void IMessageBus.Send(Envelope envelope)
    {
        if (envelope.Body is ProcessEnded ended)
        {
            Ended?.Invoke(ended);
            return;
        }
        if (!_handlers.TryGetValue(envelope.Body.GetType(), out var handler))
            return; // no local adapter: the command is for a remote system
        if (!_executedCommands.TryAdd(envelope.MessageId, 0))
            return; // idempotent consumer
        _ = Task.Run(async () =>
        {
            try
            {
                await handler(envelope.Body, new CommandContext(this, envelope));
            }
            catch (Exception e)
            {
                Faulted?.Invoke(envelope, e);
            }
        });
    }

    private async Task InboxLoop()
    {
        await foreach (var envelope in _inbox.Reader.ReadAllAsync(_stop.Token))
        {
            try
            {
                foreach (var r in Host.Handle(envelope)) Handled?.Invoke(r);
                Pump();
            }
            catch (Exception e)
            {
                Faulted?.Invoke(envelope, e);
            }
        }
    }

    private async Task TimerLoop()
    {
        using var tick = new PeriodicTimer(TimerResolution);
        while (await tick.WaitForNextTickAsync(_stop.Token))
            foreach (var due in Timers.TakeDue(_clock.Now))
                await Deliver(due);
    }

    private void Pump()
    {
        Outbox.Pump();
        if (_checkpointFile is not null)
            File.WriteAllText(_checkpointFile, Outbox.Checkpoint.ToString(CultureInfo.InvariantCulture));
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _inbox.Writer.TryComplete();
        foreach (var t in new[] { _inboxLoop, _timerLoop })
        {
            if (t is null) continue;
            try { await t; } catch (OperationCanceledException) { }
        }
        _stop.Dispose();
    }
}
