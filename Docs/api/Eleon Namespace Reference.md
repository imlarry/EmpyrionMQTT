# Eleon Namespace Reference


## Namespaces

- **`namespace Modding`**
- **`namespace Pda`**

## Classes

- **`class MsgChannelComparer`**
- **`class MessageData`**
  - Data for a chat message (protobuf-ready)

## Enumerations

- **`enum class SenderType : byte { 
  Unknown
, Player
, ServerPrio
, ServerInfo
, 
  ServerForward
, System

 }`**
  - Specifies who sends the message
- **`enum class MsgChannel : byte { 
  Global
, Faction
, Alliance
, SinglePlayer
, 
  Server

 }`**
  - Specifies the receiver(s) of a message - and thus, in which tab of the chat window UI the content will be displayed
