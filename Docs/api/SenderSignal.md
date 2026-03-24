# Eleon.Modding.SenderSignal Struct Reference


## Public Attributes

- **`string Name`**
- **`VectorInt3? BlockPos`**
  - Position inside structure if it's a block sourced signal, null else (e.g. for Control Panel signals)
- **`int Index`**
  - Usually 0, if block contains more than one send signal the consecutive index
