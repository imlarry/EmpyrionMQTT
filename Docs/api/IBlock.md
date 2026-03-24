# Eleon.Modding.IBlock Interface Reference


## Public Member Functions

- **`void Set (int? type=null, int? shape=null, int? rotation=null, bool? active=false)`**
- **`void Get (out int type, out int shape, out int rotation, out bool active)`**
- **`int GetDamage ()`**
- **`void SetDamage (int damage)`**
- **`int GetHitPoints ()`**
- **`void GetTextures (out int top, out int bottom, out int north, out int south, out int west, out int east)`**
- **`void SetTextures (int? top, int? bottom, int? north, int? south, int? west, int? east)`**
  - Per side you can set a texture if specified [Network packet per specified side!]
- **`void SetTextureForWholeBlock (int texIdx)`**
  - Sets a texture on all sides of a block [Network]
- **`void GetColors (out int top, out int bottom, out int north, out int south, out int west, out int east)`**
- **`void SetColors (int? top, int? bottom, int? north, int? south, int? west, int? east)`**
  - Per side you can set a color if specified [Network packet per specified side!]
- **`void SetColorForWholeBlock (int colorIdx)`**
  - Sets a color on all sides of a block [Network]
- **`bool? GetSwitchState (int index=0)`**
  - Only for switch / lever blocks: determine switch state
- **`bool? SetSwitchState (bool newState, int index=0)`**
  - Only for switch / lever blocks: set switch state

## Properties

- **`int? LockCode [get, set]`**
  - Gets or sets the lock code of a device block (null has the meaning of 'no lock code'). Performance note: A code change will be propagated via network.
- **`string CustomName [get]`**
  - Returns the custom name of the device block if it has one, null else
