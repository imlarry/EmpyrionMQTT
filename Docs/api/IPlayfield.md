# Eleon.Modding.IPlayfield Interface Reference


## Public Member Functions

- **`int SpawnEntity (string entityType, Vector3 pos, Quaternion rot)`**
- **`int SpawnPrefab (string prefabName, Vector3 pos)`**
- **`void RemoveEntity (int entityId)`**
- **`bool LockStructureDevice (int structureId, VectorInt3 posInStruct, bool doLock, LockResultCallback resultHandler)`**
  - Locks a device (e.g. a container) - always do this before accessing it [Network if called on client]
- **`bool IsStructureDeviceLocked (int structureId, VectorInt3 posInStruct)`**
- **`int AddVoxelArea (Vector3 pos, int sizeInMeter)`**
- **`bool MoveVoxelArea (int id, Vector3 pos)`**
- **`bool RemoveVoxelArea (int id)`**
- **`int SpawnTestPlayer (Vector3 pos)`**
  - Spawns a player for testing / debugging
- **`bool RemoveTestPlayer (int entityId)`**
  - Removes a previously test-spawned player
- **`float GetTerrainHeightAt (float x, float z)`**
  - Returns the height of the terrain (without considering player changes) at the specified position

## Properties

- **`string Name [get]`**
- **`string PlayfieldType [get]`**
- **`string PlanetType [get]`**
- **`string PlanetClass [get]`**
- **`string SolarSystemName [get]`**
- **`VectorInt3 SolarSystemCoordinates [get]`**
- **`bool IsPvP [get]`**
  - Returns if current playfield is PvP or PvE
- **`Dictionary< int, IPlayer > Players [get]`**
  - All player entities on playfield - key: entity id, value: player instance
- **`Dictionary< int, IEntity > Entities [get]`**
  - All entities on playfield (includes players) - key: entity id, value: entity instance

## Events

- **`EntityDelegate OnEntityLoaded`**
  - Event is raised each time an entity (e.g. player or structure) got loaded into the playfield
- **`EntityDelegate OnEntityUnloaded`**
  - Event is raised each time an entity (e.g. player or structure) got unloaded from the playfield
