# IMessageBus -- Next Steps

These steps describe how to move the existing system from direct IMessenger usage to IMessageBus.
They are ordered by dependency: each step builds on the previous one.

---

## Step 1: Introduce BusBuilder at each participant entry point

Each participant (ESB mod, EDNA client) currently creates a `Messenger` and calls
`ConnectAsync` directly. Replace that startup sequence with a `BusBuilder` that
produces an `IMessageBus`.

The participant type, broker address, and credentials that are currently scattered
through startup code become `BusBuilder` configuration calls. The result is a single
`IMessageBus` instance that the participant passes to everything that needs it.

---

## Step 2: Convert one handler as a proof of concept

Pick a simple, self-contained handler (a read-only request/response with no side
effects is ideal). Re-implement it as a `[BusRoute]`-decorated class implementing
`IRequestHandler<TReq, TRes>` or `IEventHandler<T>`. Define the payload POCOs.

Verify the handler registers, receives requests, and responds correctly alongside
the existing IMessenger-based handlers. Nothing else changes yet -- this is a
parallel path while the old code still runs.

---

## Step 3: Define payload model types

The existing handlers extract fields from raw JSON. Before converting more handlers,
define C# POCOs for the payloads those handlers send and receive. These become the
shared contracts between bus participants.

Payload types should live in a location accessible to both the ESB mod and the
EDNA client (either a shared project or duplicated per participant if the build
boundary requires it).

---

## Step 4: Convert remaining handlers incrementally

Working handler by handler, replace raw JSON extraction with typed handler classes
and POCO payloads. Each converted handler:

- Gets a `[BusRoute]` attribute matching its current dispatch key scope and operation.
- Implements `IRequestHandler<TReq, TRes>` or `IEventHandler<T>`.
- Replaces its direct `IMessenger.RegisterHandler` and `SubscribeBrokerAsync` calls.

The old handler registration code is removed once the typed equivalent is verified.

---

## Step 5: Wire DI for handlers that have dependencies

Handlers that currently receive dependencies via constructor or field injection need
those dependencies available through the `IServiceProvider` supplied to `BusBuilder`.
Configure the DI container to register each handler type and its dependencies, then
pass the built `IServiceProvider` to `BusBuilder.WithServiceProvider`.

---

## Step 6: Migrate EDNA client subscriptions

EDNA currently subscribes to events using `SubscribeBrokerAsync` with raw callbacks.
Replace those subscriptions with `IMessageBus.OnEvent<T>` registrations (either on
the builder before connect, or dynamically after connect for cases that are currently
registered at runtime).

Typed payload POCOs from Step 3 replace inline JSON field access throughout the
EDNA skills and services.

---

## Step 7: Replace remaining direct IMessenger call sites

Any remaining calls to `IMessenger.SendAsync`, `RequestAsync`, or `PublishRetainedAsync`
in application-level code (not in the bus implementation itself) are replaced with the
corresponding `IMessageBus` methods. After this step, application code no longer
holds a reference to `IMessenger` directly -- only `IMessageBus`.

`IMessenger` becomes an implementation detail held only by `BusBuilder` and `MessageBus`.

---

## Step 8: Expand test coverage

As each handler is converted, add a corresponding typed integration test under
`ESBTests/Bus/` following the pattern established in `Test_Bus_Integration.cs`.
Tests at this level verify the full round-trip: payload serialization, dispatch,
handler logic, and response.

---

## Deferred: middleware and per-message DI scope

Once the above steps are complete and the system is running cleanly on `IMessageBus`,
the remaining capabilities from the design can be introduced as needed:

- **Middleware pipeline**: cross-cutting concerns such as logging, validation, and
  retry added as a processing stage between the bus and the handler.
- **Per-message DI scope**: an `IServiceScope` created per handler invocation so that
  scoped services (e.g. per-request database sessions) are supported.
- **Log method**: a `Log(level, message)` convenience on `IMessageBus` that routes
  to the existing `App/log/` topic convention.
