# SharedUtils Class Reference


## Static Public Member Functions

- **`static void LogObsolete (string yamlName, string section, string param, string msg, bool isError=false)`**
  - Inform about using obsolete yaml parameters - to be able to remove them at some time...
- **`static Vector3 ReadVector3 (this BinaryReader br)`**
- **`static void Write (this BinaryWriter bw, Vector3 v)`**
- **`static void Write< T > (this BinaryWriter bw, List< T > data, Action< T > write)`**
- **`static List< T > ReadList< T > (this BinaryReader br, Func< T > read)`**
- **`static void SetPropertyDefaultValues (this object obj)`**
  - Initialize class properties with DefaultValue. Call in constructor of a class where you use DefaultValue attributes.
