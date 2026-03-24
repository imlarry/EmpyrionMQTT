# Eleon.Pda.ActionData Class Reference

_Inherits Eleon.Pda.IParentLink._


## Public Member Functions

- **`ActionData ()`**
- **`ActionData (BinaryReader br)`**
- **`void Write (BinaryWriter bw)`**
- **`void Read (BinaryReader br)`**

## Public Attributes

- **`ulong LongId`**
- **`ulong ParentId`**

## Properties

- **`string ActionTitle [get, set]`**
- **`string Description [get, set]`**
- **`bool DisplayTitle [get, set]`**
- **`GameEventType Check [get, set]`**
- **`List< string > Types [get, set]`**
- **`ListRequirement Required [get, set]`**
- **`List< string > Names [get, set]`**
- **`ListRequirement NamesRequired [get, set]`**
- **`int Amount [get, set]`**
- **`int Value [get, set]`**
- **`bool IsOptional [get, set]`**
  - If true this action does not need to be completed to complete the containing Task
- **`bool IsInvisible [get, set]`**
  - If true this action will not be visible in UI
- **`bool IsOrdered [get, set]`**
  - If true this is an "ordered action" - ordered actions have to be completed one after the other (similar to tasks)
- **`bool Internal_AllowIncompleting [get, set]`**
- **`bool IncrementCounter [get, set]`**
- **`string CompletedMessage [get, set]`**
- **`bool AllowManualCompletion [get, set]`**
- **`int SetTimer [get, set]`**
- **`float TimerRate [get, set]`**
- **`WaveStartData WaveStart [get, set]`**
- **`List< string > RequiredInventory [get, set]`**
- **`GuidingType Guiding [get, set]`**
- **`int GuidingDistance = -1 [get, set]`**
- **`int TriggerDistance [get, set]`**
- **`SignalDescriptor OnCompleteSignal [get, set]`**
- **`ulong Parent [get]`**
- **`ulong Parent [get]`**
