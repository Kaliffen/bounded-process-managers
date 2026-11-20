# Compile-Time Bounded Process Managers — POC

A working implementation of *Compile-Time Bounded Process Managers: A Join-Calculus DSL for
Event-Sourced Messaging Finite State Machines* (G. Young, draft 17 Nov 2026). It includes the restricted DSL,
the four-stage compiler, the static lifetime analysis, and an event-sourced runtime, all on local
infrastructure: an in-memory or JSON-lines event store, a virtual or wall clock, and an in-process bus.

```
dotnet build                                   # also runs the bpm compiler; DSL violations fail the build
dotnet test                                    # engine + both sample domains (~150 tests)
dotnet run --project samples/Demo -- <scenario>
    happy | retry | refund | kill              # order process, traced step by step (virtual clock)
    shipping                                   # carrier booking + delivery tracking
    pipeline                                   # order -> shipping integration -> delivery, end to end
    chaos                                      # 300 orders flowing into shipping, every fault injected
    drain                                      # deploy v2 while v1 instances drain on their pinned table
    live                                       # real wall clock, LocalRuntime, in-process adapters
    all
dotnet build tests/Fixtures/BrokenDefinition   # watch the compiler reject invalid workflows
```

## Layout

```
src/
  Bpm.Core       DSL (Flow<TState>, ProcessManager.Define), AST, compiler, analysis, doc generators
  Bpm.Runtime    pure engine (decide/evolve), event stores, ProcessHost, outbox, timers, LocalRuntime
  Bpm.Testing    Simulation (virtual clock + fault injection), InvariantChecker,
                 ReferenceInterpreter + DifferentialTester (source-vs-compiled conformance)
  Bpm.Cli        `bpm compile <assembly>`: compile all [BoundedProcess] definitions, write docs, exit 1 on errors
build/
  Bpm.targets    import into a domain project: runs bpm after the C# build
samples/
  OrderFulfillment(.Tests)   the paper's example, v1 + v2
  Shipping(.Tests)           CarrierBooking -> DeliveryTracking (two PMs, chained via a message)
  Demo                       host composing both domains; fake external systems; integration wiring
tests/
  Bpm.Tests                  engine tests with their own fixtures (no dependency on samples)
  Fixtures/BrokenDefinition  invalid on purpose; not in the solution
```

Dependency rules:
- A domain depends only on `Bpm.Core`.
- A domain's tests add `Bpm.Runtime` and `Bpm.Testing`.
- Only a host composes domains.
- Domains never reference each other; integration is a message, wired in the host.

## Adding a process manager / domain

1. **Create a domain library** that references only the DSL and imports the build step:

   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
     <ItemGroup><ProjectReference Include="..\..\src\Bpm.Core\Bpm.Core.csproj" /></ItemGroup>
     <Import Project="..\..\build\Bpm.targets" />
   </Project>
   ```

2. **Messages and folded state** are plain records. The correlation id travels on the envelope,
   so messages don't need a special interface. See `samples/Shipping/Messages.cs`.

3. **Definition**, as a static member marked `[BoundedProcess]`:

   ```csharp
   using static Bpm.Dsl.Flow<DeliveryState>;

   [BoundedProcess]
   public static ProcessDefinition Definition { get; } =
       ProcessManager.Define<DeliveryState>("DeliveryTracking", maxBudget: 21.Days())
           .StartedBy<ShipmentDispatched>(m => new DeliveryState(m.ShipmentId, m.Carrier, m.TrackingNumber))
           .Evolve<DeliveryAttemptFailed>((s, _) => s with { FailedAttempts = s.FailedAttempts + 1 })
           .Then(Retry(maxAttempts: 3, backoff: 1.Days(), attempt: ...).OnExhausted(...));
   ```

   Run `dotnet build`. You get `generated/<Name>.v1.{table.md,states.mmd,spec.md,analysis.json}`,
   or `file(line): error BPMxxx` messages. Commit the generated files and gate CI with
   `dotnet build -p:BpmCheck=true`, so behaviour changes show up as reviewed diffs.

4. **Tests.** Create a test project referencing the domain, `Bpm.Runtime` and `Bpm.Testing`. See `samples/Shipping.Tests`:
   - **Fakes** for the external systems: `sim.Service<TCommand>((cmd, ctx) => ctx.Reply(reply, delay))`.
   - **Scenario tests:** `new Simulation(registry).WithYourFakes()`, `Inject`, `Run`, then assert outcomes,
     lifetimes and `ExternalEffects`.
   - **Conformance:** `DifferentialTester.Run(definition, startMessage, vocabulary, seed)`. Include
     unrelated messages in the vocabulary.
   - **Chaos:** `new Simulation(registry, FaultProfile.Chaos, seed)`, then `InvariantChecker.Check(...)`
     must return no violations. Add domain invariants such as "refunded at most once".

5. **Hosting.** Register the definition in a `ProcessRegistry`, then run it either:
   - for real time: `new LocalRuntime(registry, store).On<TCommand>(adapter).Start()`, or
   - for simulation: a `Simulation`.

   Wire cross-domain integrations on `ProcessEnded` (see `Demo/FakeServices.cs`).

Rules the compiler and runtime enforce (or detect):
- Every wait has a finite timeout.
- Retries have static bounds.
- No loops.
- One message type per join.
- The worst-case wait must fit the budget.

Rules you must follow yourself; replay verification catches violations in tests:
- `Evolve`, predicates and command factories are pure. No clock, random or I/O.
- When you change behaviour, bump `version:`. A changed table under the same version is refused
  for existing instances (the table-hash pin).

## The paper's example, as written here

```csharp
ProcessManager.Define<OrderState>("OrderFulfillment", maxBudget: 10.Minutes())
    .StartedBy<OrderSubmitted>(m => new OrderState(m.OrderId, m.Items, m.Total))
    .Evolve<InventoryReserved>((s, _) => s with { HasInventory = true })
    .Evolve<PaymentCaptured>((s, m) => s with { HasPayment = true, PaymentId = m.PaymentId })
    .StartWith(ctx => [new ReserveInventory(ctx.OrderId, ctx.Items), new ChargePayment(ctx.OrderId, ctx.Total)])
    .Then(
        Join.All(
                On<InventoryReserved>("Inv"),
                Retry(maxAttempts: 2, backoff: 30.Seconds(),
                    step: On<PaymentCaptured>(), onAttemptFailed: On<PaymentDeclined>(),
                    reissue: s => new ChargePayment(s.OrderId, s.Total), name: "Pay"))
            .Named("InvPay")
            .WithTimeout(5.Minutes())                       // without this it is not a Step: does not type-check
            .OnMatched(Complete("Fulfilled"))
            .OnTimeout(
                Branch(s => s.HasPayment && !s.HasInventory,
                    then: Publish(s => new RefundPayment(s.OrderId, s.PaymentId!)).Then(Fail("TimeoutAfterPayment_Refunded")),
                    @else: Fail("TimeoutBeforePayment"))));
```

`bpm: OrderFulfillment v1 ok - 12 states, 38 transitions, T_DAG=5m, T_max=5m7s (budget 10m)`: T_DAG = 5 minutes, as §5.1 predicts.

## Paper → code map

| Paper | Where |
|---|---|
| §3.1 type-state builder (`UnboundedJoin --WithTimeout--> BoundedJoin`) | `Bpm.Core/Dsl/Flow.cs` |
| §3.1 "AST validation is the actual safety boundary" | `Compiler/Validator.cs` runs on the raw, public AST; tests bypass the builder |
| §3.2 constructs, plus Delay and `Join.Any` | `Dsl/Ast.cs`, `Dsl/Flow.cs` |
| §3.3 non-restarting horizon, generation-tagged timers, Kill | `Compiler/Lowering.cs`, `Runtime/Engine.cs` |
| §4 validation → join lowering → determinization → analysis | `Validator`, `Lowering`, `Determinizer`, `Analysis` |
| §4.2 duplicates, late messages | `Engine` (persisted consumed set, `InputIgnored` audit events) |
| §5.1–5.3 weighted DAG, budget, `T_max ≤ T_DAG + kδ + rε` | `Compiler/Analysis.cs`; δ and ε declared via `.Assuming(...)` |
| §6.3 deterministic replay | `Engine.VerifyReplay` |
| §6.4 source/target correspondence | `Bpm.Testing/ReferenceInterpreter.cs` + `DifferentialTester` |
| §7.1 version pinning, drain horizons | `InstanceStarted` pins version + table hash; `ProcessRegistry.Pinned`; `DrainPlanner` |
| §7.3 generated docs, CI drift gate | `bpm compile`; `-p:BpmCheck=true` |
| §7.4 / §8.3 fault injection | `Bpm.Testing/Simulation.cs`, chaos tests in every test project |
| §8 compile-time analyzer | `build/Bpm.targets`, a post-compile MSBuild step with canonical `file(line): error BPMxxx` output |
| §9 chained bounded instances | `CarrierBooking` → `ShipmentDispatched` → `DeliveryTracking` |

## Runtime architecture

- **Pure core** (`Engine`): `Decide(table, state, input, now) → events` and `Apply(state, event) → state`.
  Guards and command factories see the state after the input has been folded in.
- **The stream is the outbox.** Effects are materialized into the same atomic append as the state change:
  deterministic UUIDv5 ids, payloads, timer generation and deadline. Replay never dispatches anything.
  `OutboxDispatcher` tails the global log and is idempotent by effect id.
- **Timers are messages.** Deadlines come from the recorded decision time. The timer schedule is derived
  state, rebuilt from the instance streams on startup.
- **Routing.** A message goes to every definition that reacts to its type, keyed by correlation id.
  A definition's start message creates an instance.
- **Hosts.**
  - `LocalRuntime` uses the wall clock and an in-process channel, with timers and the outbox checkpoint
    surviving restarts when used with `FileEventStore`.
  - `Simulation` uses a deterministic virtual clock and injects network faults, timer skew, append
    failures, crashes and restarts.

## Where the paper had gaps, and what this POC does

1. **Continuations are `Step`s, not lambdas.** A lambda could pick a continuation from runtime data,
   which would defeat static analysis. Runtime data only selects among known continuations via `Branch`.
2. **The folded state must be declared** (`StartedBy` / `Evolve`). The paper's predicates read state it never defines.
3. **Retry arms name the command they re-issue** (`reissue:`).
4. **The §4.1 decline row is ambiguous.** Here, a decline schedules the backoff timer, and the backoff
   timer then re-issues the command. The paper's table is partial; the generated table covers every case.
5. **Retry exhaustion inside a join** resolves the join early through `OnTimeout`.
6. **Horizon cost is counted once.** Partial-join states share one horizon: intra-scope edges cost 0,
   and the scope's exit edge costs the horizon.
7. **Guards are not simplified against the control state.**
8. **Unexpected inputs are audited, not buffered** (the paper's multiset *M*). Replies match by type
   within an instance, not by causation id.

## Limitations

- **No mechanized proofs.** Properties are checked by compiler invariants, differential testing
  and fault injection.
- **No Roslyn analyzer.** A raw-AST definition is caught by the post-build step, not the IDE.
- **Single process.** No broker, no snapshotting. The per-instance dedup set is bounded only by the
  instance's lifetime.
- **The build hook only works inside this repo** (`BpmCliPath` can point elsewhere). Packaging the CLI
  as a dotnet tool or NuGet is the next step for standalone domain repos.
- **No parent/child supervision** and **no data-dependent joins** (e.g. "N replies where N comes from the order").
