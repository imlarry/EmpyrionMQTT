# Eleon.Pda.ChapterActivationData Class Reference


## Public Types

- **`enum class MessageType { Info
, MessageBox
, Auto
, None
 }`**

## Public Member Functions

- **`ChapterActivationData ()`**
- **`ChapterActivationData (BinaryReader br)`**
- **`void CheckAndConvertFromLegacy ()`**
- **`void Write (BinaryWriter bw)`**

## Properties

- **`GameEventType ActivationType [get, set]`**
- **`int Distance [get, set]`**
- **`bool? ListVisibility [get, set]`**
- **`GameEventType Check [get, set]`**
- **`List< string > Types [get, set]`**
- **`ListRequirement Required [get, set]`**
- **`List< string > Names [get, set]`**
- **`ListRequirement NamesRequired [get, set]`**
- **`int Amount [get, set]`**
- **`int Value [get, set]`**
- **`List< string > RequiredInventory [get, set]`**
- **`GuidingType Guiding [get, set]`**
- **`int GuidingDistance = -1 [get, set]`**
- **`int TriggerDistance [get, set]`**
- **`string NotifyMessage [get, set]`**
- **`bool NoSkip [get, set]`**
- **`bool PopupActivatesChapter [get, set]`**
- **`MessageType ActivationMessageType [get, set]`**
- **`bool HighPrio [get, set]`**
- **`int MessageTime [get, set]`**
