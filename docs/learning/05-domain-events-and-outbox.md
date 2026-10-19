# 05 — Domain events and the transactional outbox

## What was built

A way for one module to react to something another module did, without calling it directly (ARCHITECTURE §3.3, ADR-011):

- `Entity`, the base class with a `GuidV7` id. It collects events: `Raise(new ResidentCheckedIn(...))`.
- `IDomainEvent`, the event marker, and `[Reliable]`, for events whose handlers must not be skipped.
- `IDomainEventHandler<T>`, registered by `AddDomainEventHandlers(assembly)`, and `DomainEventDispatcher`, which runs the handlers for an event's type.
- `DomainEventInterceptor`, the third interceptor on every module DbContext.
  - Ordinary events run in-process, in a fresh scope, right after a successful save.
  - Reliable events become `OutboxMessage` rows in `outbox.outbox_message`, inserted by the same `SaveChanges` and so in the same transaction.
- `OutboxProcessor`, which delivers messages. It claims each one with `FOR UPDATE SKIP LOCKED` and runs its handlers as the tenant that raised it. A failure is retried after 10 s, 20 s, 40 s and so on (capped at an hour), and the message is dead-lettered after 10 attempts.
- Hangfire on PostgreSQL (schema `hangfire`):
  - The processor is the recurring job `notifications:outbox`, every 10 s, and is also enqueued right after any save that wrote messages.
  - The dashboard at `/jobs` is behind the `PlatformAdmin` policy, which for now allows Development only.

## Why it is shaped this way

**Why events at all.** When a resident checks in, Billing must create the first invoice and Notifications must send a welcome SMS. If Residents called both, it would depend on them and grow with every new reaction. With an event, Residents only says what happened, and each module decides what to do about it.

**Why an outbox instead of "save, then send".** If the process crashes between committing the check-in and enqueueing the invoice job, the invoice is lost. If it enqueues first and the save then fails, an invoice is made for a check-in that never happened. Writing the event as a row in the same transaction removes both cases: the change and its event commit or roll back together. Delivery happens afterwards and is retried until it succeeds. This is *at-least-once* delivery, so reliable handlers must be idempotent.

**Why not everything through the outbox.** A row, a poll and JSON for every event is slow for things that may be lost harmlessly, such as refreshing a cached count. In-process events cost nothing. Their handler failures are logged rather than thrown, because the user's action has already succeeded.

**Why `SKIP LOCKED` and one transaction per message.** The 10-second job and the immediate run can overlap. Skipping locked rows lets two runs share the work without delivering the same message twice at once. A slow or failing message also doesn't hold up the rest.

**Why a dead-letter flag, not endless retries.** A handler that fails ten times over many hours needs a person: bad data, or a gateway that's down. Retrying forever would hide it and waste work.

## Read in this order

1. `src/BuildingBlocks/Basera.Shared/Domain/Entity.cs` and `ReliableAttribute.cs`
2. `src/BuildingBlocks/Basera.Shared/Events/DomainEventInterceptor.cs`
3. `src/BuildingBlocks/Basera.Shared/Outbox/OutboxProcessor.cs`
4. `src/BuildingBlocks/Basera.Shared/Jobs/JobsServiceCollectionExtensions.cs`
5. `tests/Basera.IntegrationTests/Events/OutboxTests.cs`

## Questions you should be able to answer

1. `PaymentRecorded` is `[Reliable]` and its SMS handler fails twice before the gateway recovers. When are the SMS attempts made? Why must the handler check whether it already sent the SMS?
2. A service opens an explicit transaction, saves an entity that raised an ordinary (in-process) event, then rolls back. What goes wrong, and why does the same mistake not affect a `[Reliable]` event?
