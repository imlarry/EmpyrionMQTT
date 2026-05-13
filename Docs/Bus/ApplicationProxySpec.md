# Application Handler and Client Object Specification

## Overview

This specification describes a refactored implementation of the Application handler and its corresponding client object (POCO proxy) for the EmpyrionMQTT ESB system. The goal is to provide an efficient, object-oriented interface for interacting with the Application agent via the message bus.

The handler is designed to support batched operations where possible, reducing round-trips for multiple property accesses. The client object presents a familiar .NET object interface, with properties that lazily fetch data and methods that invoke operations asynchronously.

For complex sub-objects (e.g., LocalPlayer), the implementation returns descriptors rather than nested objects. These descriptors can be used with dedicated handler/client pairs for those entities.

## Handler Specification

### Class: `AppHandler`

Location: `ESB/TopicHandlers/AppHandler.cs`

#### Responsibilities
- Handle incoming requests for Application state and operations
- Support both individual and batched property retrieval
- Provide efficient access to Application data
- Return descriptors for sub-entities rather than full objects

#### Registered Operations

| Operation | Message Type | Description |
|-----------|--------------|-------------|
| `GetProperties` | Req | Returns all current Application properties in a single response |
| `GetProperty` | Req | Returns a specific property value |
| `SendChatMessage` | Req | Sends a chat message |
| `ShowDialogBox` | Req | Shows a dialog box |
| `GetAllPlayfields` | Req | Returns list of playfield descriptors |
| `GetPfServerInfos` | Req | Returns playfield server information |
| `GetPlayerEntityIds` | Req | Returns list of player entity ID descriptors |
| `GetBlockAndItemMapping` | Req | Returns block and item mapping data |
| `GetPathFor` | Req | Returns path information |
| `GetPlayerDataFor` | Req | Returns player data descriptor |
| `GetStructure` | Req | Returns structure descriptor |
| `GetStructures` | Req | Returns list of structure descriptors |

#### Operation Details

##### `GetProperties`
- **Payload**: Empty or optional filter array of property names
- **Response**: JSON object containing all requested Application properties
- **Properties Included**:
  - `GameTicks`: long
  - `Mode`: string (enum value)
  - `State`: string (enum value)
  - `ModApiProperties`: object with availability flags
  - `PlayfieldDescriptors`: array of playfield descriptors
  - `PlayerEntityIdDescriptors`: array of player entity ID descriptors
  - `PfServerInfos`: array of playfield server info objects

##### `SendChatMessage`
- **Payload**: `SendChatMessageRequest` with `Text`, optional `Channel`, `SenderType`, `SenderEntityId`, `RecipientEntityId`, `IsTextLocaKey`, `Arg1`, `Arg2`, `SenderNameOverride`
- **Response**: JSON object `{ "ok": true }` on success

##### `ShowDialogBox`
- **Payload**: `ShowDialogBoxRequest` with optional `PlayerEntityId`, `TitleText`, `BodyText`, `ButtonTexts`, `CloseOnLinkClick`, `ButtonIdxForEsc`, `ButtonIdxForEnter`, `MaxChars`, `Placeholder`, `InitialContent`, `CustomValue`
- **Response**: JSON object `{ "ok": true }` on success

##### `GetStructures`
- **Payload**: `GetStructuresRequest` with optional `PlayfieldName`, `FactionId`, `FactionGroup`, `EntityType`
- **Response**: JSON array of structure descriptors

##### Other Operations
- Follow existing patterns, returning descriptors where appropriate

#### Descriptor Format

For sub-entities, return descriptors instead of full objects:

```json
{
  "EntityType": "Player",
  "EntityId": "12345",
  "Descriptor": {
    "Name": "PlayerName",
    "EntityId": 12345
  }
}
```

This allows the client to instantiate appropriate proxy objects (e.g., `PlayerProxy`) using the descriptor.

## Client Object Specification

### Interface: `IApplicationProxy`

Location: `ESB/Proxies/IApplicationProxy.cs`

#### Properties
- `long GameTicks { get; }` - Gets current game ticks
- `string Mode { get; }` - Gets current application mode
- `string State { get; }` - Gets current application state
- `ModApiProperties ModApiProperties { get; }` - Gets ModAPI availability
- `IReadOnlyList<PlayfieldDescriptor> Playfields { get; }` - Gets playfield descriptors
- `IReadOnlyList<PlayerEntityIdDescriptor> PlayerEntityIds { get; }` - Gets player entity ID descriptors
- `IReadOnlyList<PfServerInfo> PfServerInfos { get; }` - Gets playfield server infos

#### Methods
- `Task SendChatMessageAsync(SendChatMessageRequest request)`
- `Task ShowDialogBoxAsync(ShowDialogBoxRequest request)`
- `Task<IReadOnlyList<PlayfieldInfoResponse>> GetAllPlayfieldsAsync()`
- `Task<BlockAndItemMapping> GetBlockAndItemMappingAsync()`
- `Task<GetPathForResponse> GetPathForAsync(string appFolder)`
- `Task<PlayerDataDescriptor> GetPlayerDataForAsync(int entityId)`
- `Task<StructureDescriptor> GetStructureAsync(int entityId)`
- `Task<IReadOnlyList<StructureDescriptor>> GetStructuresAsync(string playfieldName = null, byte? factionId = null, byte? factionGroup = null, string entityType = null)`

### Class: `ApplicationProxy`

Location: `ESB/Proxies/ApplicationProxy.cs`

Implements `IApplicationProxy`

#### Constructor
- `ApplicationProxy(IMessageBus bus, TimeSpan? timeout = null)`

#### Implementation Notes
- Properties use lazy loading with caching (optional TTL)
- Methods are async and return typed objects
- Descriptors are converted to appropriate types
- Error handling: throws `BusException` for timeouts/failures

#### Caching Strategy
- Properties cache values for 1 second by default
- `RefreshAsync()` method forces fresh data fetch
- Cache can be disabled for real-time scenarios

#### Cache Implementation Details
- Maintain a `_lastUpdate` DateTime and `_cachedState` JObject
- For each property access:
  1. Check if `_lastUpdate` is null or `DateTime.Now - _lastUpdate > _cacheTtl`
  2. If stale, send `GetProperties` request and update cache
  3. Return value from `_cachedState`
- Thread-safe using locks for cache access
- `RefreshAsync()` sets `_lastUpdate` to null to force next access to fetch

## Interaction Flow

### Property Access
1. Client accesses `proxy.GameTicks`
2. If cached value is stale, proxy sends `GetProperties` request
3. Handler responds with all properties
4. Proxy updates cache and returns requested value

### Method Invocation
1. Client calls `proxy.SendChatMessageAsync(new SendChatMessageRequest { Text = "Hello" })`
2. Proxy sends `SendChatMessage` request with typed payload
3. Handler processes request and responds
4. Proxy returns completion

### Sub-Entity Access
1. Client accesses `proxy.PlayerEntityIds`
2. Proxy fetches descriptors via `GetState`
3. Client uses descriptors to create `PlayerProxy` instances
4. `PlayerProxy` interacts with Player-specific handlers

## Assumptions

- Message bus provides reliable request/response semantics
- JSON serialization uses PascalCase for property names
- Timeouts default to 5 seconds
- Handlers run on main thread (access to ModAPI) when necessary
- Client objects are thread-safe for read operations
- No concurrent property writes (Application state is read-only)

## Benefits

- **Efficiency**: Batched property fetching reduces bus traffic
- **Type Safety**: Strongly-typed interfaces prevent errors
- **Performance**: Caching minimizes redundant requests
- **Scalability**: Descriptor pattern allows lazy loading of sub-entities
- **Maintainability**: Clear separation between handler and client logic

## Future Extensions

- Add property change notifications via events
- Implement writeable properties with setter methods
- Add bulk operations for multiple entities
- Support for custom timeouts per operation</content>
<parameter name="filePath">c:\Users\imlar\source\repos\EmpyrionMQTT\Docs\ApplicationProxySpec.md