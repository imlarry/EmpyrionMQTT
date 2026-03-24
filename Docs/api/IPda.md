# Eleon.Modding.IPda Interface Reference


## Public Member Functions

- **`void SpawnDropBox (RewardData reward, Vector3 dropPosition, int dropHeight=20)`**
  - Spawns a drop container.
- **`uint CreateWaveAttack (WaveStartData waveStart)`**
  - Creates a wave attack from the provided WaveStartData
- **`void GiveReward (RewardData reward, int playerId)`**
  - Give the specified RewardData to the specified player
- **`void ShowPdaMessage (string message, float duration=5f, bool hasPrio=false, bool cleanupFirst=false, int playerId=ModConsts.InvalidEntityId)`**
  - Shows a Pda message window (bottom center of HUD)
- **`void ShowPdaDialog (string message, ModApiDialogButtons buttons)`**
  - Use to show an interactive dialog window to the player.
- **`void ShowPdaChapterBriefing (ChapterData chapterData, bool noSkipButton=false, bool noChapterActivation=false)`**
  - Use to display the Pda Chapter Briefing Window
- **`int CreateId (string title)`**
  - Creates and id from the provided title
- **`void Activate (bool reset=false)`**
  - Called to execute the script – TBD!
- **`string GetBlockName (object blockVal)`**
  - Gets the block name for the provided block value
- **`Vector3 GetBlockLocation (string name, string blockName, out Vector3 worldPos)`**
  - Gets the location of the named block in world and local coordinates
- **`Vector3 GetBlockLocation (int entityId, string blockName, out Vector3 worldPos)`**
  - Gets the location of the named block in world and local coordinates
- **`Vector3 GetPoiLocation (string poiName)`**
  - Gets the location of the named POI
- **`int GetPoiEntityId (string poiName)`**
  - Gets the entity id for the POI
- **`int SpawnPrefabAtBlock (string poiName, string blockName, string prefabName, float height)`**
  - Spawns a prefab and the location of the provided block
- **`int SpawnPrefabAtPosition (string prefabName, Vector3 position)`**
  - Spawns a prefab at the given position
- **`int SpawnEntityAtPosition (Vector3 position, string className, string faction, int height=0, bool bTerrain=true, int attachToEntity=ModConsts.InvalidEntityId)`**
  - Spawns an entity at the given position
- **`void SetMapMarker (bool activate, Vector3 position, string markerName, int distance, int playerId=ModConsts.InvalidEntityId)`**
  - Creates a map marker and gives it to playerId. NOTE: only temporary markers for now are created. TODO: add icon selection, permanent markers, and more!
- **`VectorInt3 GetVectorInt3FromGameEvent (object inVec)`**
  - Helper function to unbox a vector object, typically from a GameEvent
- **`void CreateTimer (string id, float duration, Action timerAction, bool bImmediate=false)`**
  - Creates a timer
- **`void StartTimer (string id)`**
  - Use to manually start a timer
- **`void StopTimer (string id)`**
  - Use to stop a timer
- **`void KillAllTimers ()`**
  - Stops all running timers for the script
- **`IPlayfield GetPlayfieldByPlanetType (string planetType)`**
