# Eleon.Pda Namespace Reference


## Classes

- **`class ActionData`**
- **`class ChapterData`**
- **`interface IParentLink`**
- **`class PdaData`**
- **`class RewardData`**
- **`class SignalDescriptor`**
- **`class WaveStartData`**
- **`class RepeatData`**
- **`class ChapterActivationData`**
- **`class ChapterNearPoiUnitData`**
- **`class ChapterCategoryComparer`**
- **`class TaskData`**

## Enumerations

- **`enum class ChapterRestriction { 
  Undefined
, Always
, ByLevel
, WhenRewarded
, 
  ByReputation
, ByPlayfield
, ByPlayfieldType
, ChapterActivation
, 
  PdaReferral
, ByCredits
, Never
, WhenChecked
, 
  WhileCompleted

 }`**
- **`enum class ChapterCategory { 
  Undefined
, Tutorial
, FAQ
, SoloMission
, 
  FactionMission
, PolarisMission
, TalonMission
, ZiraxMission
, 
  UCHMission
, JourneyBook

 }`**
- **`enum class RewardType { 
  Item
, XP
, UP
, LevelIncrease
, 
  LevelTarget
, ReputationTarget
, Reputation
, DropBox

 }`**
- **`enum class ScenarioEventType { 
  StartMatch
, GameStart
, MissionFailed
, MissionSuccess
, 
  GameReset
, ResetHud
, NpcCorePlaced
, NpcCoreDestroyed
, 
  PlayerCorePlaced
, PlayerCoreDestroyed
, PlayerJoined
, PlayerLeft
, 
  CapturedTimerUpdate
, PdaTaskChanged
, PdaActionCompleted
, PdaProgressAction
, 
  EntityKilled
, WaveDestroyed
, UpdateActionTimer

 }`**
- **`enum class ListRequirement { Undefined
, NeedAll
, NeedOne
 }`**
- **`enum class GuidingType { 
  Default
, Destination
, Beacon
, TempIndoor
, 
  Waypoint
, DiscardWaypoint

 }`**
