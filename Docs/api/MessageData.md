# MessageData Class Reference


## Public Member Functions

- **`MessageData ()`**
- **`MessageData (BinaryReader br)`**
- **`MessageData (MessageData other)`**
- **`void Write (BinaryWriter bw)`**
- **`void Read (BinaryReader br)`**

## Public Attributes

- **`SenderType SenderType`**
- **`MsgChannel Channel`**
- **`int SenderEntityId = Eleon.Modding.ModApi.InvalidEntityId`**
- **`FactionData SenderFaction`**
- **`string SenderNameOverride`**
- **`int RecipientEntityId = Eleon.Modding.ModApi.InvalidEntityId`**
- **`FactionData RecipientFaction`**
- **`string Text`**
- **`bool IsTextLocaKey`**
- **`string Arg1`**
