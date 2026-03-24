# Eleon.Modding.ModApi Class Reference


## Static Public Member Functions

- **`static void Log (string text)`**
- **`static void Log (string text, params object[] args)`**
- **`static void LogWarning (string text)`**
- **`static void LogWarning (string text, params object[] args)`**
- **`static void LogError (string text)`**
- **`static void LogError (string text, params object[] args)`**
- **`static void OnGameEvent (GameEventType type, object arg1=null, object arg2=null, object arg3=null, object arg4=null, object arg5=null)`**

## Public Attributes

- **`const int InvalidEntityId = -1`**

## Static Public Attributes

- **`static IPlayfield Playfield`**
- **`static INetwork Network`**
- **`static IGui GUI`**
- **`static IPda PDA`**
- **`static IScript Scripting`**
- **`static ISoundPlayer SoundPlayer`**
- **`static IApplication Application`**

## Events

- **`static GameEventDelegate GameEvent`**
