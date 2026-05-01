using Eleon.Modding;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ESB.EventHandlers
{
    public partial class GameEventHandler
    {
        class ArgDef
        {
            internal readonly string Name;
            internal readonly bool IsNumeric;
            internal readonly Func<object, ContextData, JToken> Transform;

            internal ArgDef(string name, bool isNumeric = false, Func<object, ContextData, JToken> transform = null)
            {
                Name = name;
                IsNumeric = isNumeric;
                Transform = transform;
            }
        }

        class EventDef
        {
            internal readonly ArgDef[] Args;

            internal EventDef(params ArgDef[] args)
            {
                Args = args;
            }
        }

        // Resolves an item/block numeric ID to its display name; falls back to the raw int.
        static JToken ItemNameOrId(object val, ContextData ctx)
        {
            int id;
            if (!int.TryParse(val.ToString(), out id))
                return new JValue(val.ToString());
            var mapping = ctx.GameManager.BlockAndItemMapping;
            string name;
            if (mapping != null && mapping.TryGetValue(id, out name))
                return new JValue(name);
            return new JValue(id);
        }

        static readonly HashSet<GameEventType> _suppressedEvents =
            new HashSet<GameEventType>
        {
            GameEventType.HoldingItem,
        };

        static readonly Dictionary<GameEventType, EventDef> _eventDefs =
            new Dictionary<GameEventType, EventDef>
        {
            [GameEventType.StarClassEntered] = new EventDef(
                new ArgDef("StarClass"),
                new ArgDef("PlanetType")),

            [GameEventType.PlayerStatChanged] = new EventDef(
                new ArgDef("StatName"),
                new ArgDef("Previous", isNumeric: true),
                new ArgDef("Current",  isNumeric: true)),

            [GameEventType.HoldingItem] = new EventDef(
                new ArgDef("ItemName", transform: ItemNameOrId),
                new ArgDef("Ammo", isNumeric: true)),

            [GameEventType.WindowOpened] = new EventDef(
                new ArgDef("WindowName")),

            [GameEventType.WindowClosed] = new EventDef(
                new ArgDef("WindowName")),

            [GameEventType.GameStarted] = new EventDef(
                new ArgDef("Arg1"),
                new ArgDef("Arg2"),
                new ArgDef("Scenario"),
                new ArgDef("GameType")),

            [GameEventType.ArmorEquipped] = new EventDef(
                new ArgDef("ItemName", transform: ItemNameOrId),
                new ArgDef("ArmorSlot", isNumeric: true)),

            [GameEventType.ArmorUnequipped] = new EventDef(
                new ArgDef("ItemName", transform: ItemNameOrId),
                new ArgDef("ArmorSlot", isNumeric: true)),

            [GameEventType.ArmorBoostEquipped] = new EventDef(
                new ArgDef("ItemName", transform: ItemNameOrId),
                new ArgDef("BoostSlot", isNumeric: true)),

            [GameEventType.ArmorBoostUnequipped] = new EventDef(
                new ArgDef("ItemName", transform: ItemNameOrId),
                new ArgDef("BoostSlot", isNumeric: true)),

            [GameEventType.InventoryOpened] = new EventDef(
                new ArgDef("PlayerNameId"),
                new ArgDef("ContainerStructureNameId"),
                new ArgDef("Arg3"),
                new ArgDef("Pos"),
                new ArgDef("ContainerType")),
        };
    }
}
