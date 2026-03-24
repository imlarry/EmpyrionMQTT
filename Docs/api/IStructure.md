# Eleon.Modding.IStructure Interface Reference


## Public Member Functions

- **`string[] GetAllCustomDeviceNames ()`**
  - Returns an array of all custom names (as set by player and displayed in Control Panel's device list)
- **`List< VectorInt3 > GetDevicePositions (string customDeviceName)`**
- **`IDevicePosList GetDevices (DeviceTypeName name)`**
- **`T GetDevice< T > (int x, int y, int z)`**
- **`T GetDevice< T > (string blockName)`**
- **`T GetDevice< T > (VectorInt3 pos)`**
- **`T GetDevice< T > (Vector3 pos)`**
- **`IBlock GetBlock (VectorInt3 pos)`**
- **`IBlock GetBlock (int x, int y, int z)`**
- **`void SetFaction (FactionGroup group, int entityId)`**
- **`List< IStructure > GetDockedVessels ()`**
  - Get all docked vessels
- **`List< IPlayer > GetPassengers ()`**
  - List of passenger players of the ship - empty if none (always empty in bases)
- **`List< SenderSignal > GetBlockSignals (string filter=null)`**
  - Get all block sourced sender signals (if filter is specified: a signal name needs to contain the filter string to be reported)
- **`List< SenderSignal > GetControlPanelSignals ()`**
  - Get the Control Panel defined signals
- **`List< SignalFunction > GetSignalReceivers (string name)`**
  - Check if and which blocks listen to a specific signal, return list of receivers of given signal name
- **`bool GetSignalState (string name)`**
  - Get current signal state
- **`string GetSendSignalName (VectorInt3 pos)`**
  - Signal name or null if block at pos has no send signal setup
- **`Vector3 StructToGlobalPos (VectorInt3 structPos)`**
  - Convert a position of a structure or a block inside to a position on the playfield
- **`VectorInt3 GlobalToStructPos (Vector3 globalPos)`**
  - Convert a position on the playfield to a structure position or a block inside

## Properties

- **`VectorInt3 MinPos [get]`**
- **`VectorInt3 MaxPos [get]`**
- **`int Id [get]`**
  - Playfield-wide unique id of the structure (this is not the entity id)
- **`bool IsReady [get]`**
- **`bool IsPowered [get]`**
  - Returns structure's main power state
- **`bool IsOfflineProtectable [get]`**
- **`float DamageLevel [get]`**
  - Total structure damage: 0 = not damaged at all .. 1 = completely damaged
- **`int BlockCount [get]`**
- **`int DeviceCount [get]`**
- **`int LightCount [get]`**
- **`int TriangleCount [get]`**
- **`float Fuel [get]`**
- **`int PowerOutCapacity [get]`**
- **`int PowerConsumption [get]`**
- **`string PlayerCreatedSteamId [get]`**
  - SteamId of player that created the structure, else null
- **`CoreType CoreType [get]`**
- **`int SizeClass [get]`**
- **`bool IsShieldActive [get]`**
- **`int ShieldLevel [get]`**
- **`float TotalMass [get]`**
- **`bool HasLandClaimDevice [get]`**
- **`ulong LastVisitedTicks [get]`**
- **`IStructureTank FuelTank [get]`**
- **`IStructureTank OxygenTank [get]`**
- **`IStructureTank PentaxidTank [get]`**
  - Note: Can be null if ship contains no Pentaxid tank
- **`IEntity Entity [get]`**
- **`IPlayer Pilot [get]`**
  - Pilot player of the ship or null if none (always null in bases)

## Events

- **`SignalChangedEventHandler SignalChanged`**
  - Register to get notified when any signal has changed
