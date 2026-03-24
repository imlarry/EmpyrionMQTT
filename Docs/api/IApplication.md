# Eleon.Modding.IApplication Interface Reference


## Public Member Functions

- **`string GetPathFor (AppFolder appFolder)`**
- **`IPlayfieldDescr[] GetAllPlayfields ()`**
- **`Dictionary< int, List< string > > GetPfServerInfos ()`**
  - Dedi: will report data of all non-idle PfServer processes, PfServer: will report its own data
- **`IEnumerable< int > GetPlayerEntityIds ()`**
  - Get all known player entity ids (on dedi this may include offline players)
- **`PlayerData? GetPlayerDataFor (int playerEntityId)`**
  - Get some core data about a specific player, returns null if player not found
- **`void SendChatMessage (MessageData chatMsgData)`**
  - Send a chat message to the game
- **`bool ShowDialogBox (int playerEntityId, DialogConfig config, DialogActionHandler actionHandler, int customValue)`**
  - Show new dialog box for given player, dialog box reports click events to given actionHandler. Returns false if playerEntityId is invalid. If dialog could not be displayed on client side the actionHandler will be called with no reported click (buttonIdx = -1 AND linkId ="") customValue = for request / answer matching
- **`bool GetStructure (int entityId, Action< GlobalStructureInfo > resultCallback)`**
  - Request a specific structure info via its entity id from the DB
- **`bool GetStructures (string playfieldName, FactionData? factionData, EntityType? entityType, Action< IEnumerable< GlobalStructureInfo >> resultCallback)`**
  - Request infos for all structures from the DB that match all non-null filter parameters. NOTE: You have to specify playfieldName or factionData, they must not both be null.
- **`Dictionary< string, int > GetBlockAndItemMapping ()`**
  - Returns mapping of name to id for all blocks and items [not on dedi]

## Properties

- **`GameState State [get]`**
- **`ApplicationMode Mode [get]`**
- **`IPlayer LocalPlayer [get]`**
- **`ulong GameTicks [get]`**
  - Get current game run time as ticks (20 ticks is one real-time second)

## Events

- **`PlayfieldDelegate OnPlayfieldLoaded`**
  - Called after a playfield has been loaded
- **`PlayfieldDelegate OnPlayfieldUnloading`**
  - Called before a playfield gets unloaded
- **`UpdateDelegate Update`**
- **`UpdateDelegate FixedUpdate`**
- **`GamEnteredEventHandler GameEntered`**
  - Raised when the process enters or leaves a game
- **`ChatMessageSentEventHandler ChatMessageSent`**
  - Raised when a player sent a chat message
