# Eleon.Modding.IScript Interface Reference


## Public Member Functions

- **`ScriptDomain CreateDomain (string name)`**
- **`ScriptInstance CompileAndCreateInstance (ScriptDomain domain, string script, bool bFile=false)`**
- **`ScriptType Compile (ScriptDomain domain, string script, bool bFile=false)`**
- **`ScriptInstance CreateInstance (ScriptType scriptType)`**
- **`Coroutine StartCoroutine (IEnumerator e)`**
- **`void StopCoroutine (IEnumerator e)`**
- **`void StopCoroutine (Coroutine coroutine)`**
