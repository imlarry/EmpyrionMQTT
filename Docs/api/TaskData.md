# Eleon.Pda.TaskData Class Reference

_Inherits Eleon.Pda.IParentLink._


## Public Member Functions

- **`TaskData ()`**
- **`TaskData (BinaryReader br)`**
- **`void Write (BinaryWriter bw)`**
- **`void Read (BinaryReader br)`**

## Public Attributes

- **`ulong LongId`**
- **`ulong ParentId`**

## Properties

- **`string TaskTitle [get, set]`**
- **`bool DisplayTitle [get, set]`**
- **`string Headline [get, set]`**
- **`string PictureFile [get, set]`**
- **`float StartDelay [get, set]`**
- **`string StartMessage [get, set]`**
- **`string CompletedMessage [get, set]`**
- **`List< RewardData > Rewards [get, set]`**
- **`List< string > RewardedChapters [get, set]`**
- **`List< string > RewardedTasks [get, set]`**
- **`SignalDescriptor OnActivateSignal [get, set]`**
- **`SignalDescriptor OnDeactivateSignal [get, set]`**
- **`SignalDescriptor OnCompleteSignal [get, set]`**
- **`bool OnlyVisibleWhenRewarded [get, set]`**
  - Task is only visible in UI when it got rewarded
- **`List< string > OnActivatePlayerOps [get, set]`**
- **`List< string > OnActivatePlayfieldOps [get, set]`**
- **`List< string > OnActivateUIOps [get, set]`**
- **`List< string > OnDeactivatePlayerOps [get, set]`**
- **`List< string > OnDeactivatePlayfieldOps [get, set]`**
- **`List< string > OnDeactivateUIOps [get, set]`**
- **`List< string > OnCompletePlayerOps [get, set]`**
- **`List< string > OnCompletePlayfieldOps [get, set]`**
- **`List< string > OnCompleteUIOps [get, set]`**
- **`bool HasUniqueItems [get, set]`**
- **`List< ActionData > Actions [get, set]`**
- **`int Id [get, set]`**
- **`ulong Parent [get]`**
- **`ulong Parent [get]`**
