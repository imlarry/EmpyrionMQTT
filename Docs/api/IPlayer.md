# Eleon.Modding.IPlayer Interface Reference

_Inherits Eleon.Modding.IEntity._


## Public Member Functions

- **`bool Teleport (string playfield, Vector3 pos, Vector3 rot)`**
- **`bool Teleport (Vector3 pos)`**
- **`void DamageEntity (int damageAmount, int damageType)`**
- **`void MoveForward (float speed)`**
- **`void Move (Vector3 direction)`**
- **`void MoveStop ()`**
- **`bool LoadFromDSL ()`**

## Properties

- **`string SteamId [get]`**
- **`string StartPlayfield [get]`**
- **`byte Origin [get]`**
- **`FactionData FactionData [get]`**
- **`FactionRole FactionRole [get]`**
- **`float Health [get]`**
- **`float HealthMax [get]`**
- **`float Oxygen [get]`**
- **`float OxygenMax [get]`**
- **`float Stamina [get]`**
- **`float StaminaMax [get]`**
- **`float Food [get]`**
- **`float FoodMax [get]`**
- **`float Radiation [get]`**
- **`float RadiationMax [get]`**
- **`float BodyTemp [get]`**
- **`float BodyTempMax [get]`**
- **`int Kills [get]`**
- **`int Died [get]`**
- **`double Credits [get]`**
- **`int ExperiencePoints [get]`**
- **`int UpgradePoints [get]`**
- **`int Ping [get]`**
- **`IStructure CurrentStructure [get]`**
  - Structure in/on which the player currently stands - null if none
- **`IEntity DrivingEntity [get]`**
  - The entity that this player is piloting (or null)
- **`bool IsPilot [get]`**
  - true if this player is currently piloting an entity
- **`int HomeBaseId [get]`**
  - EntityId of Homebase
- **`string SteamOwnerId [get]`**
- **`int Permission [get]`**
- **`List< ItemStack > Toolbar [get]`**
- **`List< ItemStack > Bag [get]`**
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
