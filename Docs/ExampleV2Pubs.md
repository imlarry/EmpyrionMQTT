# Example V2 mosquitto_pub Commands

Topic format: `{appId}/Q/{subjectId}/*/{seq}`  
Use `*` in `<clientid>` to broadcast to all ESB instances. Replace `1` with any unique correlation ID.

---

## Application

```sh
# V2.Application.GameTicks
mosquitto_pub -t "Client/Q/V2.Application.GameTicks/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.GetAllPlayfields
mosquitto_pub -t "Client/Q/V2.Application.GetAllPlayfields/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.GetBlockAndItemMapping
mosquitto_pub -t "Client/Q/V2.Application.GetBlockAndItemMapping/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.GetPathFor
mosquitto_pub -t "Client/Q/V2.Application.GetPathFor/*/1" -m "{\"AppFolder\":\"SaveGame\"}" -u esbuser -P esbpass

# V2.Application.GetPfServerInfos
mosquitto_pub -t "Client/Q/V2.Application.GetPfServerInfos/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.GetPlayerDataFor
mosquitto_pub -t "Client/Q/V2.Application.GetPlayerDataFor/*/1" -m "{\"PlayerEntityId\":12345}" -u esbuser -P esbpass

# V2.Application.GetPlayerEntityIds
mosquitto_pub -t "Client/Q/V2.Application.GetPlayerEntityIds/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.GetStructure
mosquitto_pub -t "Client/Q/V2.Application.GetStructure/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Application.GetStructures
mosquitto_pub -t "Client/Q/V2.Application.GetStructures/*/1" -m "{\"PlayfieldName\":\"Akua\"}" -u esbuser -P esbpass

# V2.Application.LocalPlayer
mosquitto_pub -t "Client/Q/V2.Application.LocalPlayer/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.Mode
mosquitto_pub -t "Client/Q/V2.Application.Mode/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.ModApiProperties
mosquitto_pub -t "Client/Q/V2.Application.ModApiProperties/*/1" -m "{}" -u esbuser -P esbpass

# V2.Application.SendChatMessage
mosquitto_pub -t "Client/Q/V2.Application.SendChatMessage/*/1" -m "{\"Text\":\"Hello world\",\"Channel\":\"Global\"}" -u esbuser -P esbpass

# V2.Application.ShowDialogBox
mosquitto_pub -t "Client/Q/V2.Application.ShowDialogBox/*/1" -m "{\"PlayerEntityId\":12345,\"TitleText\":\"Notice\",\"BodyText\":\"Hello\",\"ButtonTexts\":[\"OK\"]}" -u esbuser -P esbpass

# V2.Application.State
mosquitto_pub -t "Client/Q/V2.Application.State/*/1" -m "{}" -u esbuser -P esbpass
```

---

## Block

```sh
# V2.Block.Get
mosquitto_pub -t "Client/Q/V2.Block.Get/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Block.GetColors
mosquitto_pub -t "Client/Q/V2.Block.GetColors/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Block.GetSwitchState
mosquitto_pub -t "Client/Q/V2.Block.GetSwitchState/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Index\":0}" -u esbuser -P esbpass

# V2.Block.GetTextures
mosquitto_pub -t "Client/Q/V2.Block.GetTextures/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Block.Set
mosquitto_pub -t "Client/Q/V2.Block.Set/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Type\":391}" -u esbuser -P esbpass

# V2.Block.SetColorForWholeBlock
mosquitto_pub -t "Client/Q/V2.Block.SetColorForWholeBlock/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"ColorIndex\":3}" -u esbuser -P esbpass

# V2.Block.SetColors
mosquitto_pub -t "Client/Q/V2.Block.SetColors/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Top\":3,\"Bottom\":3}" -u esbuser -P esbpass

# V2.Block.SetDamage
mosquitto_pub -t "Client/Q/V2.Block.SetDamage/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Damage\":50}" -u esbuser -P esbpass

# V2.Block.SetLockCode
mosquitto_pub -t "Client/Q/V2.Block.SetLockCode/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"LockCode\":1234}" -u esbuser -P esbpass

# V2.Block.SetSwitchState
mosquitto_pub -t "Client/Q/V2.Block.SetSwitchState/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"State\":true,\"Index\":0}" -u esbuser -P esbpass

# V2.Block.SetTextureForWholeBlock
mosquitto_pub -t "Client/Q/V2.Block.SetTextureForWholeBlock/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"TextureIndex\":7}" -u esbuser -P esbpass

# V2.Block.SetTextures
mosquitto_pub -t "Client/Q/V2.Block.SetTextures/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Top\":7,\"Bottom\":7}" -u esbuser -P esbpass
```

---

## Container

```sh
# V2.Container.AddItems
mosquitto_pub -t "Client/Q/V2.Container.AddItems/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Type\":2320,\"Count\":50}" -u esbuser -P esbpass

# V2.Container.Clear
mosquitto_pub -t "Client/Q/V2.Container.Clear/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Container.Contains
mosquitto_pub -t "Client/Q/V2.Container.Contains/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Type\":2320}" -u esbuser -P esbpass

# V2.Container.Get
mosquitto_pub -t "Client/Q/V2.Container.Get/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Container.GetTotalItems
mosquitto_pub -t "Client/Q/V2.Container.GetTotalItems/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Type\":2320}" -u esbuser -P esbpass

# V2.Container.RemoveItems
mosquitto_pub -t "Client/Q/V2.Container.RemoveItems/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Type\":2320,\"Count\":10}" -u esbuser -P esbpass

# V2.Container.SetContent
mosquitto_pub -t "Client/Q/V2.Container.SetContent/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Content\":[{\"Id\":2320,\"Count\":100,\"SlotIdx\":0,\"Ammo\":0,\"Decay\":0}]}" -u esbuser -P esbpass

# V2.Container.SetDecayFactor
mosquitto_pub -t "Client/Q/V2.Container.SetDecayFactor/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"DecayFactor\":0.5}" -u esbuser -P esbpass

# V2.Container.SetVolumeCapacity
mosquitto_pub -t "Client/Q/V2.Container.SetVolumeCapacity/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"VolumeCapacity\":2000.0}" -u esbuser -P esbpass
```

---

## Entity

```sh
# V2.Entity (properties query)
mosquitto_pub -t "Client/Q/V2.Entity/*/1" -m "{\"EntityId\":54321,\"Properties\":[\"Id\",\"Name\",\"Type\",\"Position\",\"Faction\"]}" -u esbuser -P esbpass

# V2.Entity.DamageEntity
mosquitto_pub -t "Client/Q/V2.Entity.DamageEntity/*/1" -m "{\"EntityId\":54321,\"DamageAmount\":100,\"DamageType\":1}" -u esbuser -P esbpass

# V2.Entity.Move
mosquitto_pub -t "Client/Q/V2.Entity.Move/*/1" -m "{\"EntityId\":54321,\"Direction\":{\"X\":0.0,\"Y\":0.0,\"Z\":1.0}}" -u esbuser -P esbpass

# V2.Entity.MoveForward
mosquitto_pub -t "Client/Q/V2.Entity.MoveForward/*/1" -m "{\"EntityId\":54321,\"Speed\":2.0}" -u esbuser -P esbpass

# V2.Entity.MoveStop
mosquitto_pub -t "Client/Q/V2.Entity.MoveStop/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass
```

---

## Gui

```sh
# V2.Gui.IsWorldVisible
mosquitto_pub -t "Client/Q/V2.Gui.IsWorldVisible/*/1" -m "{}" -u esbuser -P esbpass

# V2.Gui.ShowDialog
mosquitto_pub -t "Client/Q/V2.Gui.ShowDialog/*/1" -m "{\"TitleText\":\"Confirm\",\"BodyText\":\"Are you sure?\",\"ButtonTexts\":[\"Yes\",\"No\"]}" -u esbuser -P esbpass

# V2.Gui.ShowGameMessage
mosquitto_pub -t "Client/Q/V2.Gui.ShowGameMessage/*/1" -m "{\"Text\":\"Hello from ESB\",\"Prio\":1,\"Duration\":5.0}" -u esbuser -P esbpass
```

---

## Lcd

```sh
# V2.Lcd.Get
mosquitto_pub -t "Client/Q/V2.Lcd.Get/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Lcd.SetBackgroundColor
mosquitto_pub -t "Client/Q/V2.Lcd.SetBackgroundColor/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Color\":{\"R\":0.0,\"G\":0.0,\"B\":0.0,\"A\":1.0}}" -u esbuser -P esbpass

# V2.Lcd.SetFontSize
mosquitto_pub -t "Client/Q/V2.Lcd.SetFontSize/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"FontSize\":16}" -u esbuser -P esbpass

# V2.Lcd.SetText
mosquitto_pub -t "Client/Q/V2.Lcd.SetText/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Text\":\"Hello World\"}" -u esbuser -P esbpass

# V2.Lcd.SetTextColor
mosquitto_pub -t "Client/Q/V2.Lcd.SetTextColor/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Color\":{\"R\":1.0,\"G\":1.0,\"B\":1.0,\"A\":1.0}}" -u esbuser -P esbpass
```

---

## Light

```sh
# V2.Light.Get
mosquitto_pub -t "Client/Q/V2.Light.Get/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Light.SetBlinkData
mosquitto_pub -t "Client/Q/V2.Light.SetBlinkData/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"BlinkInterval\":1.0,\"BlinkLength\":0.5,\"BlinkOffset\":0.0}" -u esbuser -P esbpass

# V2.Light.SetColor
mosquitto_pub -t "Client/Q/V2.Light.SetColor/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Color\":{\"R\":1.0,\"G\":0.0,\"B\":0.0,\"A\":1.0}}" -u esbuser -P esbpass

# V2.Light.SetIntensity
mosquitto_pub -t "Client/Q/V2.Light.SetIntensity/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Intensity\":2.0}" -u esbuser -P esbpass

# V2.Light.SetLightType
mosquitto_pub -t "Client/Q/V2.Light.SetLightType/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"LightType\":\"Spot\"}" -u esbuser -P esbpass

# V2.Light.SetRange
mosquitto_pub -t "Client/Q/V2.Light.SetRange/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"Range\":10.0}" -u esbuser -P esbpass

# V2.Light.SetSpotAngle
mosquitto_pub -t "Client/Q/V2.Light.SetSpotAngle/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"SpotAngle\":45.0}" -u esbuser -P esbpass
```

---

## Pda

```sh
# V2.Pda.Activate
mosquitto_pub -t "Client/Q/V2.Pda.Activate/*/1" -m "{\"Reset\":false}" -u esbuser -P esbpass

# V2.Pda.CreateId
mosquitto_pub -t "Client/Q/V2.Pda.CreateId/*/1" -m "{\"Title\":\"MyMission\"}" -u esbuser -P esbpass

# V2.Pda.CreateWaveAttack
mosquitto_pub -t "Client/Q/V2.Pda.CreateWaveAttack/*/1" -m "{\"Name\":\"Wave1\",\"Cost\":100,\"Target\":\"Base\",\"Faction\":\"Zirax\"}" -u esbuser -P esbpass

# V2.Pda.GetBlockLocation
mosquitto_pub -t "Client/Q/V2.Pda.GetBlockLocation/*/1" -m "{\"EntityId\":54321,\"BlockName\":\"LandingPad\"}" -u esbuser -P esbpass

# V2.Pda.GetBlockName
mosquitto_pub -t "Client/Q/V2.Pda.GetBlockName/*/1" -m "{\"BlockVal\":391}" -u esbuser -P esbpass

# V2.Pda.GetPoiEntityId
mosquitto_pub -t "Client/Q/V2.Pda.GetPoiEntityId/*/1" -m "{\"PoiName\":\"Abandoned_Mine\"}" -u esbuser -P esbpass

# V2.Pda.GetPoiLocation
mosquitto_pub -t "Client/Q/V2.Pda.GetPoiLocation/*/1" -m "{\"PoiName\":\"Abandoned_Mine\"}" -u esbuser -P esbpass

# V2.Pda.GiveReward
mosquitto_pub -t "Client/Q/V2.Pda.GiveReward/*/1" -m "{\"PlayerId\":12345,\"Reward\":{\"Type\":\"Item\",\"Item\":\"SteelBlock\",\"Count\":50}}" -u esbuser -P esbpass

# V2.Pda.SetMapMarker
mosquitto_pub -t "Client/Q/V2.Pda.SetMapMarker/*/1" -m "{\"Activate\":true,\"Position\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0},\"MarkerName\":\"Target\",\"Distance\":500}" -u esbuser -P esbpass

# V2.Pda.ShowDialog
mosquitto_pub -t "Client/Q/V2.Pda.ShowDialog/*/1" -m "{\"Message\":\"Proceed?\",\"Buttons\":\"Yes_No\"}" -u esbuser -P esbpass

# V2.Pda.ShowMessage
mosquitto_pub -t "Client/Q/V2.Pda.ShowMessage/*/1" -m "{\"Message\":\"Mission started\",\"Duration\":5.0}" -u esbuser -P esbpass

# V2.Pda.SpawnDropBox
mosquitto_pub -t "Client/Q/V2.Pda.SpawnDropBox/*/1" -m "{\"Reward\":{\"Type\":\"Item\",\"Item\":\"SteelBlock\",\"Count\":10},\"DropPosition\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0},\"DropHeight\":20}" -u esbuser -P esbpass

# V2.Pda.SpawnEntityAtPosition
mosquitto_pub -t "Client/Q/V2.Pda.SpawnEntityAtPosition/*/1" -m "{\"Position\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0},\"ClassName\":\"Drone_V2\",\"Faction\":\"Zirax\"}" -u esbuser -P esbpass

# V2.Pda.SpawnPrefabAtBlock
mosquitto_pub -t "Client/Q/V2.Pda.SpawnPrefabAtBlock/*/1" -m "{\"PoiName\":\"Abandoned_Mine\",\"BlockName\":\"LandingPad\",\"PrefabName\":\"DropPod\",\"Height\":5.0}" -u esbuser -P esbpass

# V2.Pda.SpawnPrefabAtPosition
mosquitto_pub -t "Client/Q/V2.Pda.SpawnPrefabAtPosition/*/1" -m "{\"PrefabName\":\"DropPod\",\"Position\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0}}" -u esbuser -P esbpass
```

---

## Player

```sh
# V2.Player (properties query)
mosquitto_pub -t "Client/Q/V2.Player/*/1" -m "{\"Properties\":[\"Id\",\"Name\",\"Health\",\"Position\",\"Credits\"]}" -u esbuser -P esbpass

# V2.Player.DamageEntity
mosquitto_pub -t "Client/Q/V2.Player.DamageEntity/*/1" -m "{\"DamageAmount\":10,\"DamageType\":1}" -u esbuser -P esbpass

# V2.Player.Teleport
mosquitto_pub -t "Client/Q/V2.Player.Teleport/*/1" -m "{\"Pos\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0}}" -u esbuser -P esbpass
```

---

## Playfield

```sh
# V2.Playfield.Info
mosquitto_pub -t "Client/Q/V2.Playfield.Info/*/1" -m "{}" -u esbuser -P esbpass

# V2.Playfield.IsStructureDeviceLocked
mosquitto_pub -t "Client/Q/V2.Playfield.IsStructureDeviceLocked/*/1" -m "{\"StructureId\":54321,\"PosInStructure\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Playfield.MoveEntity
mosquitto_pub -t "Client/Q/V2.Playfield.MoveEntity/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0}}" -u esbuser -P esbpass

# V2.Playfield.RemoveEntity
mosquitto_pub -t "Client/Q/V2.Playfield.RemoveEntity/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Playfield.SpawnEntity
mosquitto_pub -t "Client/Q/V2.Playfield.SpawnEntity/*/1" -m "{\"EntityType\":\"BA\",\"Pos\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0}}" -u esbuser -P esbpass

# V2.Playfield.SpawnPrefab
mosquitto_pub -t "Client/Q/V2.Playfield.SpawnPrefab/*/1" -m "{\"PrefabName\":\"DropPod\",\"Pos\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0}}" -u esbuser -P esbpass
```

---

## Structure

```sh
# V2.Structure.AddTankContent
mosquitto_pub -t "Client/Q/V2.Structure.AddTankContent/*/1" -m "{\"EntityId\":54321,\"TankType\":\"Fuel\",\"Amount\":500.0}" -u esbuser -P esbpass

# V2.Structure.GetAllCustomDeviceNames
mosquitto_pub -t "Client/Q/V2.Structure.GetAllCustomDeviceNames/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Structure.GetBlockSignals
mosquitto_pub -t "Client/Q/V2.Structure.GetBlockSignals/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Structure.GetControlPanelSignals
mosquitto_pub -t "Client/Q/V2.Structure.GetControlPanelSignals/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Structure.GetDevicePositions
mosquitto_pub -t "Client/Q/V2.Structure.GetDevicePositions/*/1" -m "{\"EntityId\":54321,\"DeviceName\":\"LCD_Main\"}" -u esbuser -P esbpass

# V2.Structure.GetDockedVessels
mosquitto_pub -t "Client/Q/V2.Structure.GetDockedVessels/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Structure.GetPassengers
mosquitto_pub -t "Client/Q/V2.Structure.GetPassengers/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Structure.GetSendSignalName
mosquitto_pub -t "Client/Q/V2.Structure.GetSendSignalName/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Structure.GetSignalReceivers
mosquitto_pub -t "Client/Q/V2.Structure.GetSignalReceivers/*/1" -m "{\"EntityId\":54321,\"SignalName\":\"signal1\"}" -u esbuser -P esbpass

# V2.Structure.GetSignalState
mosquitto_pub -t "Client/Q/V2.Structure.GetSignalState/*/1" -m "{\"EntityId\":54321,\"SignalName\":\"signal1\"}" -u esbuser -P esbpass

# V2.Structure.GlobalToStructPos
mosquitto_pub -t "Client/Q/V2.Structure.GlobalToStructPos/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"X\":100.0,\"Y\":50.0,\"Z\":200.0}}" -u esbuser -P esbpass

# V2.Structure.Info
mosquitto_pub -t "Client/Q/V2.Structure.Info/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass

# V2.Structure.ScanFloor
mosquitto_pub -t "Client/Q/V2.Structure.ScanFloor/*/1" -m "{\"EntityId\":54321,\"Y\":5}" -u esbuser -P esbpass

# V2.Structure.SetFaction
mosquitto_pub -t "Client/Q/V2.Structure.SetFaction/*/1" -m "{\"EntityId\":54321,\"FactionGroup\":\"Player\",\"FactionEntityId\":12345}" -u esbuser -P esbpass

# V2.Structure.StructToGlobalPos
mosquitto_pub -t "Client/Q/V2.Structure.StructToGlobalPos/*/1" -m "{\"EntityId\":54321,\"StructPos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Structure.Tanks
mosquitto_pub -t "Client/Q/V2.Structure.Tanks/*/1" -m "{\"EntityId\":54321}" -u esbuser -P esbpass
```

---

## Teleporter

```sh
# V2.Teleporter.Get
mosquitto_pub -t "Client/Q/V2.Teleporter.Get/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10}}" -u esbuser -P esbpass

# V2.Teleporter.Set
mosquitto_pub -t "Client/Q/V2.Teleporter.Set/*/1" -m "{\"EntityId\":54321,\"Pos\":{\"x\":10,\"y\":5,\"z\":10},\"TargetEntityNameOrGroup\":\"Base_Alpha\",\"TargetPlayfield\":\"Akua\"}" -u esbuser -P esbpass
```
