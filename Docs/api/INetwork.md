# Eleon.Modding.INetwork Interface Reference


## Public Member Functions

- **`bool SendToDedicatedServer (string receiver, byte[] data, string playfieldName)`**
  - Send data via network to dedicated server mod
- **`bool SendToPlayfieldServer (string receiver, string playfieldName, byte[] data)`**
  - Send data via network to playfield server mod
- **`bool SendToPlayer (string receiver, int playerEntityId, byte[] data)`**
  - Send data via network to client mod
- **`bool RegisterReceiverForDediPackets (ModDataReceivedDelegate callback)`**
  - Register a callback for data packets received from a dedi server mod
- **`bool RegisterReceiverForPlayfieldPackets (ModDataReceivedDelegate callback)`**
  - Register a callback for data packets received from a playfield server mod
- **`bool RegisterReceiverForClientPackets (PlayerDataReceivedDelegate callback)`**
  - Register a callback for data packets from a client mod
