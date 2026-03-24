# Eleon.Modding.IModApi Interface Reference


## Public Member Functions

- **`void Log (string text)`**
- **`void LogWarning (string text)`**
- **`void LogError (string text)`**

## Properties

- **`IPlayfield ClientPlayfield [get, set]`**
  - Client mods only: Currently loaded playfield (can be null)
- **`INetwork Network [get]`**
- **`IGui GUI [get]`**
- **`IPda PDA [get, set]`**
- **`IScript Scripting [get]`**
- **`ISoundPlayer SoundPlayer [get]`**
- **`IApplication Application [get]`**

## Events

- **`GameEventDelegate GameEvent`**
