# Eleon.Modding.IEntity Interface Reference


## Public Member Functions

- **`void DamageEntity (int damageAmount, int damageType)`**
- **`void MoveForward (float speed)`**
- **`void Move (Vector3 direction)`**
- **`void MoveStop ()`**
- **`bool LoadFromDSL ()`**

## Properties

- **`int Id [get]`**
- **`string Name [get]`**
- **`FactionData Faction [get]`**
- **`Vector3 Position [get, set]`**
- **`Vector3 Forward [get]`**
- **`Quaternion Rotation [get, set]`**
- **`bool IsLocal [get]`**
- **`bool IsProxy [get]`**
- **`bool IsPoi [get]`**
- **`int BelongsTo [get]`**
  - Id of entity this entity belongs to
- **`int DockedTo [get]`**
  - Id of entity this entity is docked to
- **`EntityType Type [get]`**
- **`IStructure Structure [get]`**
