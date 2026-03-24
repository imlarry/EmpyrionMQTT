# Eleon.Modding.IContainer Interface Reference

_Inherits Eleon.Modding.IDevice._


## Public Member Functions

- **`void Clear ()`**
  - Removes all items
- **`bool Contains (int type)`**
  - Determines whether the container contains at least one item of the specified type
- **`int GetTotalItems (int type)`**
  - Returns total count of given item type, accumulates items of all slots
- **`int AddItems (int type, int count)`**
  - Tries to add 'count' items of specified type to container. Returns count that could not be added.
- **`int RemoveItems (int type, int count)`**
  - Tries to remove 'count' items of specified type from container
- **`List< ItemStack > GetContent ()`**
  - Returns all non-empty item stacks
- **`void SetContent (List< ItemStack > content)`**
  - Set raw content of container (data is not validated) - use repsonsibly

## Properties

- **`float VolumeCapacity [get, set]`**
  - Volume capacity in liters
- **`float DecayFactor [get, set]`**
  - Food decay factor, range: 1..0 (standard decay speed .. no decay at all) – NOTE: current containers can only handle 0 or 1
