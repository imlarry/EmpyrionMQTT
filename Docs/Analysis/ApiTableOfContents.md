# Empyrion Modding API - Table of Contents

_Generated from ModApi.dll and Mif.dll via DLL reflection._
_Source directory: `C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Client\Empyrion_Data\Managed`_
_Run `dotnet run --project Scripts/ExtractApi` to regenerate._

---

## Object Hierarchy

Pseudocode navigation paths showing how interfaces relate through properties and methods.

```
IModApi
  .Application : IApplication
      .GameTicks : ulong
      .LocalPlayer : IPlayer
      .Mode : ApplicationMode
      .State : GameState
      .GetAllPlayfields() : IPlayfieldDescr[]
      .GetBlockAndItemMapping() : Dictionary<string,int>
      .GetPathFor(AppFolder) : string
      .GetPfServerInfos() : Dictionary<int,List<string>>
      .GetPlayerDataFor(playerEntityId) : PlayerData?
      .GetPlayerEntityIds() : IEnumerable<int>
      .GetStructure(entityId) : GlobalStructureInfo      // async callback
      .GetStructures(playfieldName,...) : IEnumerable<GlobalStructureInfo>  // async callback
      .SendChatMessage(MessageData)
      .ShowDialogBox(playerEntityId, DialogConfig, ...)

  .ClientPlayfield : IPlayfield
      .Name : string
      .PlanetClass : string
      .PlanetType : string
      .PlayfieldType : string
      .IsPvP : bool
      .SolarSystemName : string
      .SolarSystemCoordinates : VectorInt3
      .Entities : Dictionary<int, IEntity>
          [id] : IEntity
              .Id : int
              .Name : string
              .Type : EntityType
              .Position : Vector3
              .Rotation : Quaternion
              .Forward : Vector3
              .Faction : FactionData
              .IsLocal : bool
              .IsProxy : bool
              .IsPoi : bool
              .BelongsTo : int
              .DockedTo : int
              .Structure : IStructure
                  .Id : int
                  .Entity : IEntity
                  .BlockCount : int
                  .DeviceCount : int
                  .LightCount : int
                  .CoreType : CoreType
                  .DamageLevel : float
                  .Fuel : float
                  .IsPowered : bool
                  .IsReady : bool
                  .IsShieldActive : bool
                  .ShieldLevel : int
                  .SizeClass : int
                  .TotalMass : float
                  .TriangleCount : int
                  .MaxPos, .MinPos : VectorInt3
                  .HasLandClaimDevice : bool
                  .IsOfflineProtectable : bool
                  .LastVisitedTicks : ulong
                  .PlayerCreatedSteamId : string
                  .PowerConsumption : int
                  .PowerOutCapacity : int
                  .Pilot : IPlayer
                  .FuelTank : IStructureTank
                      .Capacity : float
                      .Content : float
                      .UsesIntegerAmounts : bool
                      .AddContent(amount)
                  .OxygenTank : IStructureTank      // same shape
                  .PentaxidTank : IStructureTank    // same shape
                  .GetBlock(x, y, z) : IBlock
                      .CustomName : string
                      .LockCode : int?
                      .ParentBlock : IBlock
                      .Get(out type, out shape, out rotation, out active)
                      .Set(type?, shape?, rotation?, active?)
                      .SetDamage(damage) / GetDamage() / GetHitPoints()
                      .GetTextures(...) / SetTextures(...) / SetTextureForWholeBlock(texIdx)
                      .GetColors(...) / SetColors(...) / SetColorForWholeBlock(colorIdx)
                      .GetSwitchState(index) / SetSwitchState(state, index)
                  .GetDevice<IContainer>(name|pos) : IContainer
                      .VolumeCapacity : float
                      .DecayFactor : float
                      .GetContent() : List<ItemStack>
                      .SetContent(List<ItemStack>)
                      .AddItems(type, count) / RemoveItems(type, count)
                      .Contains(type) / GetTotalItems(type)
                      .Clear()
                  .GetDevice<ILcd>(name|pos) : ILcd
                      .GetText() / SetText(text)
                      .GetTextColor() / SetTextColor(Color)
                      .GetBackgroundColor() / SetBackgroundColor(Color)
                      .GetFontSize() / SetFontSize(fontSize)
                  .GetDevice<ILight>(name|pos) : ILight
                      .GetColor() / SetColor(Color)
                      .GetIntensity() / SetIntensity(float)
                      .GetRange() / SetRange(float)
                      .GetLightType() / SetLightType(LightType)
                      .GetSpotAngle() / SetSpotAngle(float)
                      .GetBlinkData(...) / SetBlinkData(interval, length, offset)
                  .GetDevice<ITeleporter>(name|pos) : ITeleporter
                      .TargetData : TeleporterData
                          .TargetEntityNameOrGroup : string
                          .TargetPlayfield : string
                          .TargetSolarSystemName : string
                          .Origin : byte
                  .GetAllCustomDeviceNames() : string[]
                  .GetDevicePositions(customDeviceName) : List<VectorInt3>
                  .GetDevices(DeviceTypeName) : IDevicePosList
                      .Count : int
                      .GetAt(index) : VectorInt3
                  .GetDockedVessels() : List<IStructure>
                  .GetPassengers() : List<IPlayer>
                  .GetBlockSignals(filter?) : List<SenderSignal>
                  .GetControlPanelSignals() : List<SenderSignal>
                  .GetSignalState(name) : bool
                  .GetSignalReceivers(name) : List<SignalFunction>
                  .GetSendSignalName(pos) : string
                  .SetFaction(FactionGroup, entityId)
                  .StructToGlobalPos(VectorInt3) : Vector3
                  .GlobalToStructPos(Vector3) : VectorInt3
              .DamageEntity(amount, type)
              .Move(Vector3) / .MoveForward(speed) / .MoveStop()
              .LoadFromDSL()
      .Players : Dictionary<int, IPlayer>
          [id] : IPlayer          // extends IEntity (all IEntity members apply)
              .SteamId : string
              .SteamOwnerId : string
              .StartPlayfield : string
              .Origin : byte
              .Permission : int
              .Health / .HealthMax : float
              .Oxygen / .OxygenMax : float
              .Stamina / .StaminaMax : float
              .Food / .FoodMax : float
              .Radiation / .RadiationMax : float
              .BodyTemp / .BodyTempMax : float
              .Credits : double
              .ExperiencePoints : int
              .UpgradePoints : int
              .Kills : int
              .Died : int
              .Ping : int
              .HomeBaseId : int
              .IsPilot : bool
              .FactionData : FactionData
              .FactionRole : FactionRole
              .CurrentStructure : IStructure
              .DrivingEntity : IEntity
              .Toolbar : List<ItemStack>
              .Bag : List<ItemStack>
              .Teleport(pos) / .Teleport(playfield, pos, rot)
              .DamageEntity(amount, type)
      .SpawnEntity(entityType, pos, rot) : int
      .SpawnPrefab(prefabName, pos) : int
      .RemoveEntity(entityId)
      .MoveEntity(entityId, pos)          // not on IPlayfield directly; via ESB handler
      .IsStructureDeviceLocked(structureId, posInStruct) : bool

  .GUI : IGui
      .IsWorldVisible : bool
      .ShowGameMessage(text, prio, duration)
      .ShowDialog(DialogConfig, handler, customValue)

  .PDA : IPda
      .Activate(reset)
      .CreateId(title) : int
      .CreateWaveAttack(WaveStartData) : uint
      .GiveReward(RewardData, playerId)
      .SpawnDropBox(RewardData, dropPosition, dropHeight)
      .SetMapMarker(activate, position, markerName, distance, playerId)
      .GetPoiLocation(poiName) : Vector3
      .GetPoiEntityId(poiName) : int
      .GetBlockLocation(entityId, blockName, out worldPos) : Vector3
      .GetBlockName(blockVal) : string
      .SpawnPrefabAtBlock(poiName, blockName, prefabName, height) : int
      .SpawnPrefabAtPosition(prefabName, position) : int
      .SpawnEntityAtPosition(position, className, faction, ...) : int
      .ShowPdaMessage(message, duration, hasPrio, cleanupFirst, playerId)
      .ShowPdaDialog(message, ModApiDialogButtons)
      .ShowPdaChapterBriefing(ChapterData, ...)
```

---

## ModApi.dll

### ModApi.dll - Interfaces

#### IApplication  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| GameTicks | ulong | get; |
| LocalPlayer | IPlayer | get; |
| Mode | ApplicationMode | get; |
| State | GameState | get; |

**Methods**

| Signature |
|-----------|
| `IPlayfieldDescr[] GetAllPlayfields()` |
| `Dictionary<string, int> GetBlockAndItemMapping()` |
| `string GetPathFor(AppFolder appFolder)` |
| `Dictionary<int, List<string>> GetPfServerInfos()` |
| `Nullable<PlayerData> GetPlayerDataFor(int playerEntityId)` |
| `IEnumerable<int> GetPlayerEntityIds()` |
| `bool GetStructure(int entityId, Action<GlobalStructureInfo> resultCallback)` |
| `bool GetStructures(string playfieldName, Nullable<FactionData> factionData, Nullable<EntityType> entityType, Action<IEnumerable<GlobalStructureInfo>> resultCallback)` |
| `void SendChatMessage(MessageData chatMsgData)` |
| `bool ShowDialogBox(int playerEntityId, DialogConfig config, DialogActionHandler actionHandler, int customValue)` |

**Events**

| Name | Delegate |
|------|----------|
| ChatMessageSent | ChatMessageSentEventHandler |
| FixedUpdate | UpdateDelegate |
| GameEntered | GamEnteredEventHandler |
| OnPlayfieldLoaded | PlayfieldDelegate |
| OnPlayfieldUnloading | PlayfieldDelegate |
| Update | UpdateDelegate |

#### IBlock  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| CustomName | string | get; |
| LockCode | Nullable<int> | get; set; |
| ParentBlock | IBlock | get; |

**Methods**

| Signature |
|-----------|
| `void Get(Int32& type, Int32& shape, Int32& rotation, Boolean& active)` |
| `void GetColors(Int32& top, Int32& bottom, Int32& north, Int32& south, Int32& west, Int32& east)` |
| `int GetDamage()` |
| `int GetHitPoints()` |
| `Nullable<bool> GetSwitchState(int index)` |
| `void GetTextures(Int32& top, Int32& bottom, Int32& north, Int32& south, Int32& west, Int32& east)` |
| `void Set(Nullable<int> type, Nullable<int> shape, Nullable<int> rotation, Nullable<bool> active)` |
| `void SetColorForWholeBlock(int colorIdx)` |
| `void SetColors(Nullable<int> top, Nullable<int> bottom, Nullable<int> north, Nullable<int> south, Nullable<int> west, Nullable<int> east)` |
| `void SetDamage(int damage)` |
| `Nullable<bool> SetSwitchState(bool newState, int index)` |
| `void SetTextureForWholeBlock(int texIdx)` |
| `void SetTextures(Nullable<int> top, Nullable<int> bottom, Nullable<int> north, Nullable<int> south, Nullable<int> west, Nullable<int> east)` |

#### IContainer  _(Eleon.Modding)_

_implements IDevice_

**Properties**

| Name | Type | Access |
|------|------|--------|
| DecayFactor | float | get; set; |
| VolumeCapacity | float | get; set; |

**Methods**

| Signature |
|-----------|
| `int AddItems(int type, int count)` |
| `void Clear()` |
| `bool Contains(int type)` |
| `List<ItemStack> GetContent()` |
| `int GetTotalItems(int type)` |
| `int RemoveItems(int type, int count)` |
| `void SetContent(List<ItemStack> content)` |

#### IDevice  _(Eleon.Modding)_


#### IDevicePosList  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Count | int | get; |

**Methods**

| Signature |
|-----------|
| `VectorInt3 GetAt(int index)` |

#### IEntity  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| BelongsTo | int | get; |
| DockedTo | int | get; |
| Faction | FactionData | get; |
| Forward | Vector3 | get; |
| Id | int | get; |
| IsLocal | bool | get; |
| IsPoi | bool | get; |
| IsProxy | bool | get; |
| Name | string | get; |
| Position | Vector3 | get; set; |
| Rotation | Quaternion | get; set; |
| Structure | IStructure | get; |
| Type | EntityType | get; |

**Methods**

| Signature |
|-----------|
| `void DamageEntity(int damageAmount, int damageType)` |
| `bool LoadFromDSL()` |
| `void Move(Vector3 direction)` |
| `void MoveForward(float speed)` |
| `void MoveStop()` |

#### IGui  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| IsWorldVisible | bool | get; |

**Methods**

| Signature |
|-----------|
| `bool ShowDialog(DialogConfig config, DialogActionHandler handler, int customValue)` |
| `void ShowGameMessage(string text, int prio, float duration)` |

#### ILcd  _(Eleon.Modding)_

_implements IDevice_

**Methods**

| Signature |
|-----------|
| `Color GetBackgroundColor()` |
| `int GetFontSize()` |
| `string GetText()` |
| `Color GetTextColor()` |
| `void SetBackgroundColor(Color color)` |
| `void SetFontSize(int fontSize)` |
| `void SetText(string text)` |
| `void SetTextColor(Color color)` |

#### ILight  _(Eleon.Modding)_

_implements IDevice_

**Methods**

| Signature |
|-----------|
| `void GetBlinkData(Single& blinkInterval, Single& blinkLength, Single& blinkOffset)` |
| `Color GetColor()` |
| `float GetIntensity()` |
| `LightType GetLightType()` |
| `float GetRange()` |
| `float GetSpotAngle()` |
| `void SetBlinkData(float blinkInterval, float blinkLength, float blinkOffset)` |
| `void SetColor(Color color)` |
| `void SetIntensity(float intensity)` |
| `void SetLightType(LightType lightType)` |
| `void SetRange(float range)` |
| `void SetSpotAngle(float spotAngle)` |

#### IMod  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `void Init(IModApi modAPI)` |
| `void Shutdown()` |

#### IModApi  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Application | IApplication | get; |
| ClientPlayfield | IPlayfield | get; set; |
| GUI | IGui | get; |
| Network | INetwork | get; |
| PDA | IPda | get; set; |
| Scripting | IScript | get; |
| SoundPlayer | ISoundPlayer | get; |

**Methods**

| Signature |
|-----------|
| `void Log(string text)` |
| `void LogError(string text)` |
| `void LogWarning(string text)` |

**Events**

| Name | Delegate |
|------|----------|
| GameEvent | GameEventDelegate |

#### INetwork  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `bool RegisterReceiverForClientPackets(PlayerDataReceivedDelegate callback)` |
| `bool RegisterReceiverForDediPackets(ModDataReceivedDelegate callback)` |
| `bool RegisterReceiverForPlayfieldPackets(ModDataReceivedDelegate callback)` |
| `bool SendToDedicatedServer(string receiver, Byte[] data, string playfieldName)` |
| `bool SendToPlayer(string receiver, int playerEntityId, Byte[] data)` |
| `bool SendToPlayfieldServer(string receiver, string playfieldName, Byte[] data)` |

#### IPda  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `void Activate(bool reset)` |
| `int CreateId(string title)` |
| `void CreateTimer(string id, float duration, Action timerAction, bool bImmediate)` |
| `uint CreateWaveAttack(WaveStartData waveStart)` |
| `Vector3 GetBlockLocation(string name, string blockName, Vector3& worldPos)` |
| `Vector3 GetBlockLocation(int entityId, string blockName, Vector3& worldPos)` |
| `string GetBlockName(object blockVal)` |
| `IPlayfield GetPlayfieldByPlanetType(string planetType)` |
| `int GetPoiEntityId(string poiName)` |
| `Vector3 GetPoiLocation(string poiName)` |
| `VectorInt3 GetVectorInt3FromGameEvent(object inVec)` |
| `void GiveReward(RewardData reward, int playerId)` |
| `void KillAllTimers()` |
| `void SetMapMarker(bool activate, Vector3 position, string markerName, int distance, int playerId)` |
| `void ShowPdaChapterBriefing(ChapterData chapterData, bool noSkipButton, bool noChapterActivation)` |
| `void ShowPdaDialog(string message, ModApiDialogButtons buttons)` |
| `void ShowPdaMessage(string message, float duration, bool hasPrio, bool cleanupFirst, int playerId)` |
| `void SpawnDropBox(RewardData reward, Vector3 dropPosition, int dropHeight)` |
| `int SpawnEntityAtPosition(Vector3 position, string className, string faction, int height, bool bTerrain, int attachToEntity)` |
| `int SpawnPrefabAtBlock(string poiName, string blockName, string prefabName, float height)` |
| `int SpawnPrefabAtPosition(string prefabName, Vector3 position)` |
| `void StartTimer(string id)` |
| `void StopTimer(string id)` |

#### IPlayer  _(Eleon.Modding)_

_implements IEntity_

**Properties**

| Name | Type | Access |
|------|------|--------|
| Bag | List<ItemStack> | get; |
| BodyTemp | float | get; |
| BodyTempMax | float | get; |
| Credits | double | get; |
| CurrentStructure | IStructure | get; |
| Died | int | get; |
| DrivingEntity | IEntity | get; |
| ExperiencePoints | int | get; |
| FactionData | FactionData | get; |
| FactionRole | FactionRole | get; |
| Food | float | get; |
| FoodMax | float | get; |
| Health | float | get; |
| HealthMax | float | get; |
| HomeBaseId | int | get; |
| IsPilot | bool | get; |
| Kills | int | get; |
| Origin | byte | get; |
| Oxygen | float | get; |
| OxygenMax | float | get; |
| Permission | int | get; |
| Ping | int | get; |
| Radiation | float | get; |
| RadiationMax | float | get; |
| Stamina | float | get; |
| StaminaMax | float | get; |
| StartPlayfield | string | get; |
| SteamId | string | get; |
| SteamOwnerId | string | get; |
| Toolbar | List<ItemStack> | get; |
| UpgradePoints | int | get; |

**Methods**

| Signature |
|-----------|
| `bool Teleport(Vector3 pos)` |
| `bool Teleport(string playfield, Vector3 pos, Vector3 rot)` |

#### IPlayfield  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Entities | Dictionary<int, IEntity> | get; |
| IsPvP | bool | get; |
| Name | string | get; |
| PlanetClass | string | get; |
| PlanetType | string | get; |
| Players | Dictionary<int, IPlayer> | get; |
| PlayfieldType | string | get; |
| SolarSystemCoordinates | VectorInt3 | get; |
| SolarSystemName | string | get; |

**Methods**

| Signature |
|-----------|
| `int AddVoxelArea(Vector3 pos, int sizeInMeter)` |
| `float GetTerrainHeightAt(float x, float z)` |
| `bool IsStructureDeviceLocked(int structureId, VectorInt3 posInStruct)` |
| `bool LockStructureDevice(int structureId, VectorInt3 posInStruct, bool doLock, LockResultCallback resultHandler)` |
| `bool MoveVoxelArea(int id, Vector3 pos)` |
| `void RemoveEntity(int entityId)` |
| `bool RemoveTestPlayer(int entityId)` |
| `bool RemoveVoxelArea(int id)` |
| `int SpawnEntity(string entityType, Vector3 pos, Quaternion rot)` |
| `int SpawnPrefab(string prefabName, Vector3 pos)` |
| `int SpawnTestPlayer(Vector3 pos)` |

**Events**

| Name | Delegate |
|------|----------|
| OnEntityLoaded | EntityDelegate |
| OnEntityUnloaded | EntityDelegate |

#### IPlayfieldDescr  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| IsInstance | bool | get; |
| PlayfieldName | string | get; set; |
| PlayfieldType | PlayfieldType | get; set; |

#### IPortal  _(Eleon.Modding)_

_implements ITeleporter, IDevice_

#### IScript  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `ScriptType Compile(ScriptDomain domain, string script, bool bFile)` |
| `ScriptInstance CompileAndCreateInstance(ScriptDomain domain, string script, bool bFile)` |
| `ScriptDomain CreateDomain(string name)` |
| `ScriptInstance CreateInstance(ScriptType scriptType)` |
| `Coroutine StartCoroutine(IEnumerator e)` |
| `void StopCoroutine(IEnumerator e)` |
| `void StopCoroutine(Coroutine coroutine)` |

#### ISoundPlayer  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `void CleanUp()` |
| `void PlaySound(int sourceEntityId, string clipName, SoundPlayMode playMode)` |
| `void PlaySound(Vector3 sourcePos, string clipName, SoundPlayMode playMode, int minDistance, int maxDistance, string id)` |
| `void StopSound(string id)` |

#### IStructure  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| BlockCount | int | get; |
| CoreType | CoreType | get; |
| DamageLevel | float | get; |
| DeviceCount | int | get; |
| Entity | IEntity | get; |
| Fuel | float | get; |
| FuelTank | IStructureTank | get; |
| HasLandClaimDevice | bool | get; |
| Id | int | get; |
| IsOfflineProtectable | bool | get; |
| IsPowered | bool | get; |
| IsReady | bool | get; |
| IsShieldActive | bool | get; |
| LastVisitedTicks | ulong | get; |
| LightCount | int | get; |
| MaxPos | VectorInt3 | get; |
| MinPos | VectorInt3 | get; |
| OxygenTank | IStructureTank | get; |
| PentaxidTank | IStructureTank | get; |
| Pilot | IPlayer | get; |
| PlayerCreatedSteamId | string | get; |
| PowerConsumption | int | get; |
| PowerOutCapacity | int | get; |
| ShieldLevel | int | get; |
| SizeClass | int | get; |
| TotalMass | float | get; |
| TriangleCount | int | get; |

**Methods**

| Signature |
|-----------|
| `String[] GetAllCustomDeviceNames()` |
| `IBlock GetBlock(VectorInt3 pos)` |
| `IBlock GetBlock(int x, int y, int z)` |
| `List<SenderSignal> GetBlockSignals(string filter)` |
| `List<SenderSignal> GetControlPanelSignals()` |
| `T GetDevice(string blockName)` |
| `T GetDevice(VectorInt3 pos)` |
| `T GetDevice(Vector3 pos)` |
| `T GetDevice(int x, int y, int z)` |
| `List<VectorInt3> GetDevicePositions(string customDeviceName)` |
| `IDevicePosList GetDevices(DeviceTypeName name)` |
| `List<IStructure> GetDockedVessels()` |
| `List<IPlayer> GetPassengers()` |
| `string GetSendSignalName(VectorInt3 pos)` |
| `List<SignalFunction> GetSignalReceivers(string name)` |
| `bool GetSignalState(string name)` |
| `VectorInt3 GlobalToStructPos(Vector3 globalPos)` |
| `void SetColorOfBlocks(List<BlockPosColor> posAndColors, BlockSide side)` |
| `void SetFaction(FactionGroup group, int entityId)` |
| `Vector3 StructToGlobalPos(VectorInt3 structPos)` |

**Events**

| Name | Delegate |
|------|----------|
| SignalChanged | SignalChangedEventHandler |

#### IStructureTank  _(Eleon.Modding)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Capacity | float | get; |
| Content | float | get; |
| UsesIntegerAmounts | bool | get; |

**Methods**

| Signature |
|-----------|
| `void AddContent(float amount)` |

#### ITeleporter  _(Eleon.Modding)_

_implements IDevice_

**Properties**

| Name | Type | Access |
|------|------|--------|
| TargetData | TeleporterData | get; set; |

#### IParentLink  _(Eleon.Pda)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Parent | ulong | get; |

### ModApi.dll - Classes

#### SharedUtils


**Methods**

| Signature |
|-----------|
| `void LogObsolete(string yamlName, string section, string param, string msg, bool isError)` |
| `Dictionary<K, V> ReadDict(BinaryReader br, Func<ValueTuple<K, V>> read)` |
| `List<T> ReadList(BinaryReader br, Func<T> read)` |
| `Vector3 ReadVector3(BinaryReader br)` |
| `void SetPropertyDefaultValues(object obj)` |
| `void SetPropertyDefaultValues(object obj, TmpPropertyInfo[]& Properties)` |
| `void Write(BinaryWriter bw, Vector3 v)` |
| `void Write(BinaryWriter bw, List<T> data, Action<T> write)` |
| `void Write(BinaryWriter bw, Dictionary<K, V> data, Action<KeyValuePair<K, V>> write)` |

#### MessageData  _(Eleon)_


**Fields**

| Name | Type |
|------|------|
| Arg1 | string |
| Arg2 | string |
| Channel | MsgChannel |
| GameTime | ulong |
| IsTextLocaKey | bool |
| RecipientEntityId | int |
| RecipientFaction | FactionData |
| SenderEntityId | int |
| SenderFaction | FactionData |
| SenderNameOverride | string |
| SenderType | SenderType |
| Text | string |

#### MsgChannelComparer  _(Eleon)_

_implements IEqualityComparer<MsgChannel>_

**Methods**

| Signature |
|-----------|
| `bool Equals(MsgChannel a, MsgChannel b)` |
| `int GetHashCode(MsgChannel a)` |

**Fields**

| Name | Type |
|------|------|
| Instance | MsgChannelComparer |

#### DialogConfig  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| BodyText | string |
| ButtonIdxForEnter | int |
| ButtonIdxForEsc | int |
| ButtonTexts | String[] |
| CloseOnLinkClick | bool |
| InitialContent | string |
| MaxChars | int |
| Placeholder | string |
| TitleText | string |

#### ModConsts  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| InvalidEntityId | int |
| ItemBaseId | int |
| ModEntityId | int |
| TexturesOrColorsPerTable | int |

#### ScriptDomain  _(Eleon.Modding)_


#### ScriptInstance  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `object Call(string methodName)` |
| `object Call(string methodName, Object[] arguments)` |

#### ScriptType  _(Eleon.Modding)_


#### ActionData  _(Eleon.Pda)_

_implements IParentLink_

**Properties**

| Name | Type | Access |
|------|------|--------|
| ActionTitle | string | get; set; |
| AllowManualCompletion | bool | get; set; |
| Amount | int | get; set; |
| Check | GameEventType | get; set; |
| Comment | string | get; set; |
| CompletedMessage | string | get; set; |
| Description | string | get; set; |
| DisplayTitle | bool | get; set; |
| EndData | string | get; set; |
| Guiding | GuidingType | get; set; |
| GuidingDistance | int | get; set; |
| IncrementCounter | bool | get; set; |
| Internal_AllowIncompleting | bool | get; set; |
| IsInvisible | bool | get; set; |
| IsOptional | bool | get; set; |
| IsOrdered | bool | get; set; |
| Names | List<string> | get; set; |
| NamesRequired | ListRequirement | get; set; |
| OnCompleteSignal | SignalDescriptor | get; set; |
| Parent | ulong | get; |
| Required | ListRequirement | get; set; |
| RequiredInventory | List<string> | get; set; |
| SetTimer | int | get; set; |
| TimerRate | float | get; set; |
| TriggerDistance | int | get; set; |
| Types | List<string> | get; set; |
| Value | int | get; set; |
| WaveStart | WaveStartData | get; set; |

**Methods**

| Signature |
|-----------|
| `void Read(BinaryReader br)` |
| `void Write(BinaryWriter bw)` |

**Fields**

| Name | Type |
|------|------|
| LongId | ulong |
| ParentId | ulong |
| StaticProperties | TmpPropertyInfo[] |

#### ChapterActivationData  _(Eleon.Pda)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| ActivationMessageType | MessageType | get; set; |
| ActivationType | GameEventType | get; set; |
| Amount | int | get; set; |
| Check | GameEventType | get; set; |
| Distance | int | get; set; |
| Guiding | GuidingType | get; set; |
| GuidingDistance | int | get; set; |
| HighPrio | bool | get; set; |
| ListVisibility | Nullable<bool> | get; set; |
| MessageTime | int | get; set; |
| Names | List<string> | get; set; |
| NamesRequired | ListRequirement | get; set; |
| NoSkip | bool | get; set; |
| NotifyMessage | string | get; set; |
| PopupActivatesChapter | bool | get; set; |
| Required | ListRequirement | get; set; |
| RequiredInventory | List<string> | get; set; |
| TriggerDistance | int | get; set; |
| Types | List<string> | get; set; |
| Value | int | get; set; |

**Methods**

| Signature |
|-----------|
| `void CheckAndConvertFromLegacy()` |
| `void Write(BinaryWriter bw)` |

#### ChapterCategoryComparer  _(Eleon.Pda)_

_implements IEqualityComparer<ChapterCategory>_

**Methods**

| Signature |
|-----------|
| `bool Equals(ChapterCategory a, ChapterCategory b)` |
| `int GetHashCode(ChapterCategory a)` |

**Fields**

| Name | Type |
|------|------|
| Instance | ChapterCategoryComparer |

#### ChapterData  _(Eleon.Pda)_

_implements IParentLink_

**Properties**

| Name | Type | Access |
|------|------|--------|
| ActionTimeoutHours | int | get; set; |
| Activatable | ChapterRestriction | get; set; |
| ActivateChapterOnCompletion | string | get; set; |
| ActivateChapterOnSkip | string | get; set; |
| AutoActivateOnGameStart | bool | get; set; |
| Category | ChapterCategory | get; set; |
| ChapterActivation | List<ChapterActivationData> | get; set; |
| ChapterTitle | string | get; set; |
| Comment | string | get; set; |
| CompletedMessage | string | get; set; |
| CompletedSound | string | get; set; |
| CoreTimer | int | get; set; |
| Description | string | get; set; |
| EnableChapters | List<string> | get; set; |
| Faction | string | get; set; |
| Group | string | get; set; |
| HasPlayfieldRestriction | bool | get; |
| HideTasks | bool | get; set; |
| IsUsingChapterActivation | bool | get; |
| NoSkip | bool | get; set; |
| NotParallel | bool | get; set; |
| Parent | ulong | get; |
| PdaReferral | List<string> | get; set; |
| PictureFile | string | get; set; |
| PlayerCredits | int | get; set; |
| PlayerLevel | int | get; set; |
| Playfields | List<string> | get; set; |
| PlayfieldTypes | List<string> | get; set; |
| Preamble | string | get; set; |
| RepeatConditions | RepeatData | get; set; |
| ReputationLevel | int | get; set; |
| RewardedChapters | List<string> | get; set; |
| RewardedChaptersOnSkip | List<string> | get; set; |
| Rewards | List<RewardData> | get; set; |
| RewardsOnSkip | List<RewardData> | get; set; |
| SkipMessage | string | get; set; |
| StartDelay | float | get; set; |
| StartMessage | string | get; set; |
| Tasks | List<TaskData> | get; set; |
| TaskUINotation | string | get; set; |
| Visibility | ChapterRestriction | get; set; |
| VisibleOnStartPlayfields | List<string> | get; set; |
| VisibleOnStartPlayfieldTypes | List<string> | get; set; |
| WaitForPlayers | int | get; set; |

**Methods**

| Signature |
|-----------|
| `bool ApplyOverrides(IReadOnlyDictionary<string, string> overrideData)` |
| `string GetChapterStartMessage()` |
| `void Read(BinaryReader br)` |
| `string ToString()` |
| `void Write(BinaryWriter bw)` |

**Fields**

| Name | Type |
|------|------|
| ChapterTitleHash | int |
| LongId | ulong |

#### ChapterNearPoiUnitData  _(Eleon.Pda)_


**Methods**

| Signature |
|-----------|
| `bool IsNear(Vector3 _position)` |
| `void Write(BinaryWriter _bw)` |

**Fields**

| Name | Type |
|------|------|
| Distance | float |
| IsActive | bool |
| PoiNames | List<string> |
| Position | Vector3 |
| UnitName | string |

#### PdaData  _(Eleon.Pda)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Chapters | List<ChapterData> | get; set; |
| Creator | string | get; set; |

**Methods**

| Signature |
|-----------|
| `void Read(BinaryReader br)` |
| `void Write(BinaryWriter bw)` |

#### RepeatData  _(Eleon.Pda)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Delay | short | get; set; |
| DelayAdd | short | get; set; |
| DelaySeconds | short | get; set; |
| NoReminder | bool | get; set; |
| NumRepeats | short | get; set; |

**Methods**

| Signature |
|-----------|
| `void Write(BinaryWriter bw)` |

#### RewardData  _(Eleon.Pda)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| AirDrop | bool | get; set; |
| Count | int | get; set; |
| DropBox | String[] | get; set; |
| DropRange | int | get; set; |
| Faction | String[] | get; set; |
| Item | string | get; set; |
| Meta | int | get; set; |
| Type | RewardType | get; set; |

**Methods**

| Signature |
|-----------|
| `void Write(BinaryWriter bw)` |

#### SignalDescriptor  _(Eleon.Pda)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Name | string | get; set; |
| Type | BlockSignalType | get; set; |
| Value | bool | get; |

**Methods**

| Signature |
|-----------|
| `void Write(BinaryWriter bw)` |

#### TaskData  _(Eleon.Pda)_

_implements IParentLink_

**Properties**

| Name | Type | Access |
|------|------|--------|
| Actions | List<ActionData> | get; set; |
| Comment | string | get; set; |
| CompletedMessage | string | get; set; |
| DisplayTitle | bool | get; set; |
| HasUniqueItems | bool | get; set; |
| Headline | string | get; set; |
| Id | int | get; set; |
| OnActivatePlayerOps | List<string> | get; set; |
| OnActivatePlayfieldOps | List<string> | get; set; |
| OnActivateSignal | SignalDescriptor | get; set; |
| OnActivateUIOps | List<string> | get; set; |
| OnCompletePdaDataOps | List<string> | get; set; |
| OnCompletePlayerOps | List<string> | get; set; |
| OnCompletePlayfieldOps | List<string> | get; set; |
| OnCompleteSignal | SignalDescriptor | get; set; |
| OnCompleteUIOps | List<string> | get; set; |
| OnDeactivatePlayerOps | List<string> | get; set; |
| OnDeactivatePlayfieldOps | List<string> | get; set; |
| OnDeactivateSignal | SignalDescriptor | get; set; |
| OnDeactivateUIOps | List<string> | get; set; |
| OnlyVisibleWhenRewarded | bool | get; set; |
| Parent | ulong | get; |
| PictureFile | string | get; set; |
| RewardedChapters | List<string> | get; set; |
| RewardedTasks | List<string> | get; set; |
| Rewards | List<RewardData> | get; set; |
| StartDelay | float | get; set; |
| StartMessage | string | get; set; |
| TaskTitle | string | get; set; |

**Methods**

| Signature |
|-----------|
| `void Read(BinaryReader br)` |
| `void Write(BinaryWriter bw)` |

**Fields**

| Name | Type |
|------|------|
| LongId | ulong |
| ParentId | ulong |

#### WaveStartData  _(Eleon.Pda)_


**Properties**

| Name | Type | Access |
|------|------|--------|
| Cost | int | get; set; |
| Faction | string | get; set; |
| Name | string | get; set; |
| Target | string | get; set; |

**Methods**

| Signature |
|-----------|
| `void Write(BinaryWriter bw)` |

### ModApi.dll - Structs

#### FactionData


**Methods**

| Signature |
|-----------|
| `void Read(BinaryReader br)` |
| `string ToString()` |
| `void Write(BinaryWriter bw)` |

**Fields**

| Name | Type |
|------|------|
| Group | FactionGroup |
| Id | int |

#### BlockPosColor  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| ColorIndex | int |
| Pos | VectorInt3 |

#### PlayerData  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| EntityId | int |
| IsOnline | bool |
| PlayerName | string |
| PlayfieldName | string |
| SteamId | string |

#### SenderSignal  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| BlockPos | Nullable<VectorInt3> |
| Index | int |
| Name | string |

#### SignalFunction  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| Behavior | SignalBehaviour |
| BlockPos | VectorInt3 |
| Func | BlockFunction |
| IsInverting | bool |

#### TeleporterData  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| Origin | byte |
| TargetEntityNameOrGroup | string |
| TargetPlayfield | string |
| TargetSolarSystemName | string |

#### VectorInt3  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `Vector3 CenterToVector3()` |
| `bool Equals(VectorInt3 other)` |
| `bool Equals(object other)` |
| `int GetHashCode()` |
| `string ToString()` |

**Fields**

| Name | Type |
|------|------|
| Undef | VectorInt3 |
| x | int |
| y | int |
| z | int |

### ModApi.dll - Enums

#### BlockFunction

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 3 | Open |
| 1 | Powered |
| 0 | Undefined |
| 2 | Unlock |

#### BlockSide

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Bottom |
| 5 | East |
| 2 | North |
| 4 | South |
| 0 | Top |
| 3 | West |

#### BlockSignalType

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 0 | None |
| 2 | Off |
| 1 | On |
| 3 | OnOff |

#### CoreType

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 2 | Admin |
| -1 | NoData |
| 3 | NoFaction |
| 4 | NoFactionAdmin |
| 0 | None |
| 5 | NPC |
| 6 | NPCAdmin |
| 1 | Player |

#### DeviceTypeName

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 0 | All_NOT_USED_YET |
| 1 | AmmoCntr |
| 2 | Container |
| 3 | Fridge |
| 4 | HarvestCntr |
| 5 | LCD |
| 6 | Light |
| 7 | Portal |
| 8 | Teleporter |

#### EntityType

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 9 | Animal |
| 21 | Asteroid |
| 6 | AstRes |
| 28 | AstRingPlanet |
| 7 | AstVoxel |
| 2 | BA |
| 22 | Civilian |
| 3 | CV |
| 23 | Cyborg |
| 17 | DropContainer |
| 15 | EnemyDrone |
| 8 | EscapePod |
| 18 | ExplosiveDevice |
| 5 | HV |
| 11 | Item |
| 25 | MissionContainer |
| 29 | NPCFighter |
| 1 | Player |
| 16 | PlayerBackpack |
| 19 | PlayerBike |
| 20 | PlayerBikeFolded |
| 12 | PlayerDrone |
| 26 | Proxy |
| 4 | SV |
| 27 | TerrainPlaceable |
| 13 | Trader |
| 24 | TroopTransport |
| 10 | Turret |
| 14 | UnderRes |
| 0 | Unknown |

#### FactionGroup

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 5 | Admin |
| 8 | Alien |
| 13 | Civilian |
| 9 | DynFaction1 |
| 18 | DynFaction10 |
| 10 | DynFaction2 |
| 11 | DynFaction3 |
| 12 | DynFaction4 |
| 13 | DynFaction5 |
| 14 | DynFaction6 |
| 15 | DynFaction7 |
| 16 | DynFaction8 |
| 17 | DynFaction9 |
| 0 | Faction |
| 10 | Kriel |
| 254 | Main |
| 255 | NoFaction |
| 9 | Pirates |
| 1 | Player |
| 7 | Polaris |
| 3 | Predator |
| 4 | Prey |
| 6 | Talon |
| 12 | Trader |
| 11 | UCH |
| 2 | Zirax |

#### FactionRole

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Admin |
| 3 | Apprentice |
| 0 | Founder |
| 2 | Member |
| 4 | NotSet |

#### GameEventType

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 89 | ArmorBoostEquipped |
| 90 | ArmorBoostUnequipped |
| 87 | ArmorEquipped |
| 88 | ArmorUnequipped |
| 63 | AttackedBlock |
| 37 | AttackedEntity |
| 66 | AttackWave |
| 81 | BiomeChanged |
| 65 | BlockChanged |
| 18 | BlockDestroyed |
| 8 | BlocksPlaced |
| 17 | BlocksRemoved |
| 7 | ConstructionQueueContains |
| 73 | CreatedAstVoxel |
| 54 | CreatedBase |
| 57 | CreatedCV |
| 56 | CreatedGV |
| 55 | CreatedSV |
| 77 | DayNightChange |
| 20 | DeviceNamePowered |
| 19 | DevicePowered |
| 21 | DeviceUsed |
| 72 | DialogOption |
| 61 | DrilledOrFilledBlocks |
| 40 | EnterGVCockpit |
| 39 | EnterSVCockpit |
| 46 | EscapePodHitGround |
| 74 | GameEnded |
| 32 | GameStarted |
| 86 | HoldingItem |
| 2 | InventoryClosed |
| 83 | InventoryClosedPoi |
| 4 | InventoryContains |
| 5 | InventoryContainsCountOfItem |
| 3 | InventoryEmptied |
| 1 | InventoryOpened |
| 82 | InventoryOpenedPoi |
| 14 | ItemsConsumed |
| 16 | ItemsCrafted |
| 12 | ItemsPickedUp |
| 15 | ItemsUnlocked |
| 13 | ItemsUsed |
| 38 | KilledEntity |
| 59 | LowFood |
| 58 | LowOxygen |
| 22 | MainPowerSwitched |
| 26 | NearPoi |
| 25 | NearResource |
| 27 | NearUnit |
| 0 | None |
| 69 | NpcCorePlaced |
| 50 | OpenedBlueprintFactory |
| 47 | OpenedBlueprintLibCreative |
| 49 | OpenedBlueprintLibSaveOverwrite |
| 48 | OpenedBlueprintLibSurvival |
| 52 | OpenedConstructor |
| 51 | OpenedEnergyNeedingObject |
| 53 | OpenedOxygenGenerator |
| 62 | OresDropped |
| 41 | PlacedConstructor |
| 42 | PlacedFoodProcessor |
| 43 | PlacedFuelTankInBase |
| 44 | PlacedGeneratorInBase |
| 45 | PlacedMedicinelabMS |
| 60 | PlantHarvested |
| 33 | PlayerDied |
| 34 | PlayerLevelUp |
| 76 | PlayerStatChanged |
| 30 | PlayfieldEntered |
| 78 | PlayfieldLeft |
| 31 | PlayfieldTypeEntered |
| 79 | PlayfieldTypeLeft |
| 24 | PoiDiscovered |
| 23 | ResourceDiscovered |
| 68 | SetTimer |
| 35 | SettingChanged |
| 70 | ShieldAttacked |
| 29 | Signal |
| 75 | StarClassEntered |
| 36 | StatusEffectApplied |
| 80 | StatusEffectRemoved |
| 64 | StructureInEnemyTerritory |
| 84 | StructureProduced |
| 9 | StructureSpawned |
| 28 | SubjectKilled |
| 6 | ToolbarContains |
| 85 | ViewSelected |
| 71 | WaitAction |
| 67 | WaveDestroyed |
| 11 | WindowClosed |
| 10 | WindowOpened |

#### PlayfieldType

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 5 | GasGiant |
| 4 | Moon |
| 1 | Planet |
| 2 | Space |
| 3 | Sun |
| 0 | Undefined |

#### SignalBehaviour

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Follow |
| 4 | Off |
| 3 | On |
| 2 | Toggle |
| 0 | Undefined |

#### MsgChannel  _(Eleon)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 2 | Alliance |
| 1 | Faction |
| 0 | Global |
| 4 | Server |
| 3 | SinglePlayer |

#### SenderType  _(Eleon)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Player |
| 4 | ServerForward |
| 3 | ServerInfo |
| 2 | ServerPrio |
| 5 | System |
| 0 | Unknown |

#### AppFolder  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 4 | ActiveScenario |
| 5 | Cache |
| 1 | Content |
| 6 | Dedicated |
| 3 | Mod |
| 0 | Root |
| 2 | SaveGame |

#### ApplicationMode  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Client |
| 2 | DedicatedServer |
| 3 | PlayfieldServer |
| 0 | SinglePlayer |

#### GameState  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Loading |
| 0 | NotRunning |
| 2 | Running |

#### ModApiDialogButtons  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 8 | Accept_Decline |
| 1 | Cancel |
| 7 | LetsGo |
| 9 | None |
| 0 | Ok |
| 3 | Ok_Cancel |
| 2 | Quit |
| 4 | Set_Cancel |
| 6 | Skip_LetsGo |
| 5 | Yes_No |

#### ModApiDialogPosition  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 2 | Cancel |
| 0 | Left |
| 1 | Mid |
| 0 | Positive |
| 2 | Right |

#### SoundPlayMode  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 2 | Loop |
| 3 | LoopEnd |
| 1 | LoopStart |
| 4 | LoopStop |
| 0 | OneShot |

#### ChapterCategory  _(Eleon.Pda)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 4 | FactionMission |
| 2 | FAQ |
| 9 | JourneyBook |
| 5 | PolarisMission |
| 3 | SoloMission |
| 6 | TalonMission |
| 1 | Tutorial |
| 8 | UCHMission |
| 0 | Undefined |
| 7 | ZiraxMission |

#### ChapterRestriction  _(Eleon.Pda)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Always |
| 9 | ByCredits |
| 2 | ByLevel |
| 5 | ByPlayfield |
| 6 | ByPlayfieldType |
| 4 | ByReputation |
| 7 | ChapterActivation |
| 13 | MultiplayerOnly |
| 10 | Never |
| 0 | None |
| 8 | PdaReferral |
| 11 | WhenChecked |
| 3 | WhenRewarded |
| 12 | WhileCompleted |

#### GuidingType  _(Eleon.Pda)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 2 | Beacon |
| 0 | Default |
| 1 | Destination |
| 5 | DiscardWaypoint |
| 3 | TempIndoor |
| 4 | Waypoint |

#### ListRequirement  _(Eleon.Pda)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | NeedAll |
| 2 | NeedOne |
| 0 | Undefined |

#### RewardType  _(Eleon.Pda)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 7 | DropBox |
| 0 | Item |
| 3 | LevelIncrease |
| 4 | LevelTarget |
| 6 | Reputation |
| 5 | ReputationTarget |
| 2 | UP |
| 1 | XP |

#### ScenarioEventType  _(Eleon.Pda)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 12 | CapturedTimerUpdate |
| 16 | EntityKilled |
| 4 | GameReset |
| 1 | GameStart |
| 2 | MissionFailed |
| 3 | MissionSuccess |
| 7 | NpcCoreDestroyed |
| 6 | NpcCorePlaced |
| 14 | PdaActionCompleted |
| 15 | PdaProgressAction |
| 13 | PdaTaskChanged |
| 9 | PlayerCoreDestroyed |
| 8 | PlayerCorePlaced |
| 10 | PlayerJoined |
| 11 | PlayerLeft |
| 5 | ResetHud |
| 0 | StartMatch |
| 18 | UpdateActionTimer |
| 17 | WaveDestroyed |

### ModApi.dll - Delegates

#### ChatMessageSentEventHandler  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (MessageData chatMsgData)`

#### DialogActionHandler  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (int buttonIdx, string linkId, string inputContent, int playerId, int customValue)`

#### EntityDelegate  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (IEntity entity)`

#### GameEventDelegate  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (GameEventType type, object arg1, object arg2, object arg3, object arg4, object arg5)`

#### GamEnteredEventHandler  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (bool hasEntered)`

#### LockResultCallback  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (int structureId, VectorInt3 posInStruct, bool success)`

#### ModDataReceivedDelegate  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (string sender, string playfieldName, Byte[] data)`

#### PlayerDataReceivedDelegate  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (string sender, int playerEntityId, Byte[] data)`

#### PlayfieldDelegate  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (IPlayfield playfield)`

#### SignalChangedEventHandler  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void (string name, bool newState, int triggeringEntityId)`

#### UpdateDelegate  _(Eleon.Modding)_

_implements ICloneable, ISerializable_

`void ()`

---

## Mif.dll

### Mif.dll - Interfaces

#### ModGameAPI  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `void Console_Write(string txt)` |
| `ulong Game_GetTickTime()` |
| `bool Game_Request(CmdId reqId, ushort seqNr, object data)` |

#### ModInterface  _(Eleon.Modding)_


**Methods**

| Signature |
|-----------|
| `void Game_Event(CmdId eventId, ushort seqNr, object data)` |
| `void Game_Exit()` |
| `void Game_Start(ModGameAPI dediAPI)` |
| `void Game_Update()` |

### Mif.dll - Classes

#### AlliancesFaction  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| faction1Id | int |
| faction2Id | int |
| isAllied | bool |

#### AlliancesTable  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| alliances | HashSet<int> |

#### BannedPlayerData  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| BannedPlayers | List<BanEntry> |

#### BlueprintResources  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| ItemStacks | List<ItemStack> |
| PlayerId | int |
| ReplaceExisting | bool |

#### ChatInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| msg | string |
| playerId | int |
| recipientEntityId | int |
| recipientFactionId | int |
| type | byte |

#### ChatMsgData  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| Channel | MsgChannel |
| RecipientEntityId | int |
| RecipientFactionGroup | int |
| RecipientFactionId | int |
| SenderEntityId | int |
| SenderFactionGroup | int |
| SenderFactionId | int |
| SenderNameOverride | string |
| SenderType | SenderType |
| Text | string |

#### ConsoleCommandInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| allowed | bool |
| command | string |
| playerEntityId | int |

#### DediStats  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| fps | float |
| mem | int |
| players | int |
| ticks | ulong |
| uptime | int |

#### DialogBoxData  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| Id | int |
| MsgText | string |
| NegButtonText | string |
| PosButtonText | string |

#### EntityExportInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| filePath | string |
| id | int |
| isForceUnload | bool |
| playfield | string |

#### EntityInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |
| pos | PVector3 |
| type | int |

#### EntitySpawnInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| entityTypeName | string |
| exportedEntityDat | string |
| factionGroup | byte |
| factionId | int |
| forceEntityId | int |
| name | string |
| playfield | string |
| pos | PVector3 |
| prefabDir | string |
| prefabName | string |
| rot | PVector3 |
| type | byte |

#### ErrorInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| errorType | ErrorType |

#### FactionChangeInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| factionGroup | byte |
| factionId | int |
| id | int |

#### FactionInfoList  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| factions | List<FactionInfo> |

#### GlobalStructureList  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| globalStructures | Dictionary<string, List<GlobalStructureInfo>> |

#### Id  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |

#### IdAndIntValue  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| Id | int |
| Value | int |

#### IdCredits  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| credits | double |
| id | int |

#### IdItemStack  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |
| itemStack | ItemStack |

#### IdList  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| list | List<int> |

#### IdMsgPrio  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |
| msg | string |
| prio | byte |
| time | float |

#### IdPlayfield  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |
| playfield | string |

#### IdPlayfieldName  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |
| name | string |
| playfield | string |

#### IdPlayfieldPositionRotation  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |
| playfield | string |
| pos | PVector3 |
| rot | PVector3 |

#### IdPositionRotation  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| id | int |
| pos | PVector3 |
| rot | PVector3 |

#### IdStructureBlockInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| blockStatistics | Dictionary<int, int> |
| id | int |

#### Inventory  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| bag | ItemStack[] |
| playerId | int |
| toolbelt | ItemStack[] |

#### ItemExchangeInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| buttonText | string |
| desc | string |
| id | int |
| items | ItemStack[] |
| title | string |

#### PlayerInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| bag | ItemStack[] |
| bodyTemp | float |
| bodyTempMax | float |
| bpInFactory | string |
| bpRemainingTime | float |
| bpResourcesInFactory | Dictionary<int, float> |
| clientId | int |
| credits | double |
| died | int |
| entityId | int |
| exp | int |
| factionGroup | byte |
| factionId | int |
| factionRole | byte |
| food | float |
| foodMax | float |
| health | float |
| healthMax | float |
| kills | int |
| origin | byte |
| oxygen | float |
| oxygenMax | float |
| permission | int |
| ping | int |
| playerName | string |
| playfield | string |
| pos | PVector3 |
| producedPrefabs | List<string> |
| radiation | float |
| radiationMax | float |
| rot | PVector3 |
| stamina | float |
| staminaMax | float |
| startPlayfield | string |
| steamId | string |
| steamOwnerId | string |
| toolbar | ItemStack[] |
| upgrade | int |

#### PlayerInfoSet  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| bodyTemp | Nullable<int> |
| bodyTempMax | Nullable<int> |
| bpRemainingTime | Nullable<float> |
| entityId | int |
| experiencePoints | Nullable<int> |
| factionGroup | Nullable<byte> |
| factionId | Nullable<int> |
| factionRole | Nullable<byte> |
| food | Nullable<int> |
| foodMax | Nullable<int> |
| health | Nullable<int> |
| healthMax | Nullable<int> |
| origin | Nullable<int> |
| oxygen | Nullable<int> |
| oxygenMax | Nullable<int> |
| radiation | Nullable<int> |
| radiationMax | Nullable<int> |
| sendLastNLogs | Nullable<byte> |
| stamina | Nullable<int> |
| staminaMax | Nullable<int> |
| startPlayfield | string |
| upgradePoints | Nullable<int> |

#### PlayfieldEntityList  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| entities | List<EntityInfo> |
| playfield | string |

#### PlayfieldList  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| playfields | List<string> |

#### PlayfieldLoad  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| playfield | string |
| processId | int |
| sec | float |

#### PlayfieldStats  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| chunks | int |
| devices | int |
| fps | float |
| fpsmin | float |
| mem | int |
| mobs | int |
| players | int |
| playfield | string |
| processId | int |
| proxies | int |
| structs | int |
| uptime | int |

#### PString  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| pstr | string |

#### StatisticsParam  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| int1 | int |
| int2 | int |
| int3 | int |
| int4 | int |
| type | StatisticsType |

#### TraderNPCItemSoldInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| boughtItemCount | int |
| boughtItemId | int |
| boughtItemPrice | int |
| playerEntityId | int |
| structEntityId | int |
| traderEntityId | int |
| traderType | string |

### Mif.dll - Structs

#### FactionInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| abbrev | string |
| factionId | int |
| name | string |
| origin | byte |

#### GameEventData  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| Amount | int |
| EventType | byte |
| Flag | bool |
| ItemStacks | ItemStack[] |
| Name | string |
| PlayerId | int |
| Type | int |

#### GlobalStructureInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| classNr | int |
| cntBlocks | int |
| cntDevices | int |
| cntLights | int |
| cntTriangles | int |
| coreType | sbyte |
| dockedShips | List<int> |
| factionGroup | byte |
| factionId | int |
| fuel | int |
| id | int |
| lastVisitedUTC | long |
| name | string |
| pilotId | int |
| PlayfieldName | string |
| pos | PVector3 |
| powered | bool |
| rot | PVector3 |
| Sector | PVector3 |
| SolarSystemCoord | PVector3 |
| SolarSystemName | string |
| type | byte |

#### ItemStack  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| ammo | int |
| count | int |
| decay | int |
| id | int |
| slotIdx | byte |

#### PdaStateInfo  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| Name | string |
| PlayerId | int |
| StateChange | PdaStateChange |

#### PVector3  _(Eleon.Modding)_


**Fields**

| Name | Type |
|------|------|
| x | float |
| y | float |
| z | float |

### Mif.dll - Enums

#### CmdId  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 49 | Event_AlliancesAll |
| 51 | Event_AlliancesFaction |
| 57 | Event_BannedPlayers |
| 53 | Event_ChatMessage |
| 54 | Event_ChatMessageEx |
| 72 | Event_ConsoleCommand |
| 7 | Event_Dedi_Stats |
| 62 | Event_DialogButtonIndex |
| 40 | Event_Entity_PosAndRot |
| 64 | Event_Error |
| 41 | Event_Faction_Changed |
| 75 | Event_GameEvent |
| 44 | Event_Get_Factions |
| 10 | Event_GlobalStructure_List |
| 47 | Event_NewEntityId |
| 63 | Event_Ok |
| 74 | Event_PdaStateChange |
| 16 | Event_Player_ChangedPlayfield |
| 14 | Event_Player_Connected |
| 28 | Event_Player_Credits |
| 15 | Event_Player_Disconnected |
| 35 | Event_Player_DisconnectedWaiting |
| 67 | Event_Player_GetAndRemoveInventory |
| 18 | Event_Player_Info |
| 23 | Event_Player_Inventory |
| 33 | Event_Player_ItemExchange |
| 20 | Event_Player_List |
| 69 | Event_Playfield_Entity_List |
| 3 | Event_Playfield_List |
| 0 | Event_Playfield_Loaded |
| 5 | Event_Playfield_Stats |
| 1 | Event_Playfield_Unloaded |
| 45 | Event_Statistics |
| 13 | Event_Structure_BlockStatistics |
| 65 | Event_TraderNPCItemSold |
| 48 | Request_AlliancesAll |
| 50 | Request_AlliancesFaction |
| 29 | Request_Blueprint_Finish |
| 30 | Request_Blueprint_Resources |
| 55 | Request_ConsoleCommand |
| 6 | Request_Dedi_Stats |
| 37 | Request_Entity_ChangePlayfield |
| 38 | Request_Entity_Destroy |
| 70 | Request_Entity_Destroy2 |
| 71 | Request_Entity_Export |
| 39 | Request_Entity_PosAndRot |
| 73 | Request_Entity_SetName |
| 42 | Request_Entity_Spawn |
| 36 | Request_Entity_Teleport |
| 43 | Request_Get_Factions |
| 56 | Request_GetBannedPlayers |
| 8 | Request_GlobalStructure_List |
| 9 | Request_GlobalStructure_Update |
| 59 | Request_InGameMessage_AllPlayers |
| 60 | Request_InGameMessage_Faction |
| 58 | Request_InGameMessage_SinglePlayer |
| 52 | Request_Load_Playfield |
| 46 | Request_NewEntityId |
| 27 | Request_Player_AddCredits |
| 24 | Request_Player_AddItem |
| 31 | Request_Player_ChangePlayerfield |
| 25 | Request_Player_Credits |
| 66 | Request_Player_GetAndRemoveInventory |
| 21 | Request_Player_GetInventory |
| 17 | Request_Player_Info |
| 32 | Request_Player_ItemExchange |
| 19 | Request_Player_List |
| 26 | Request_Player_SetCredits |
| 22 | Request_Player_SetInventory |
| 34 | Request_Player_SetPlayerInfo |
| 68 | Request_Playfield_Entity_List |
| 2 | Request_Playfield_List |
| 4 | Request_Playfield_Stats |
| 61 | Request_ShowDialog_SinglePlayer |
| 12 | Request_Structure_BlockStatistics |
| 11 | Request_Structure_Touch |

#### ErrorType  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 8 | BlueprintError |
| 14 | CommandNotImplemented |
| 6 | CouldNotAddItemToInventory |
| 7 | CouldNotRemoveItemFromInventory |
| 5 | EntityIdNotFound |
| 16 | EntityNotLocalToPlayfield |
| 10 | EntityTypeNotSupported |
| 15 | IOError |
| 1 | MissingParameter |
| 11 | NoIdlePlayfieldFound |
| 0 | None |
| 9 | NotEnoughCredits |
| 2 | PlayerIdNotFound |
| 13 | PlayfieldAlreadyLoaded |
| 12 | PlayfieldCannotBeLoaded |
| 4 | PlayfieldConnectionNotFound |
| 3 | PlayfieldOfPlayerNotFound |

#### MsgChannel  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 2 | Alliance |
| 1 | Faction |
| 0 | Global |
| 4 | Server |
| 3 | SinglePlayer |

#### PdaStateChange  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | ChapterActivated |
| 3 | ChapterCompleted |
| 2 | ChapterDeactivated |
| 0 | Undefined |

#### SenderType  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | Player |
| 4 | ServerForward |
| 3 | ServerInfo |
| 2 | ServerPrio |
| 5 | System |
| 0 | Unknown |

#### StatisticsType  _(Eleon.Modding)_

_implements IComparable, IFormattable, IConvertible_

| Value | Name |
|-------|------|
| 1 | CoreAdded |
| 0 | CoreRemoved |
| 2 | PlayerDied |
| 4 | StructDestroyed |
| 3 | StructOnOff |

