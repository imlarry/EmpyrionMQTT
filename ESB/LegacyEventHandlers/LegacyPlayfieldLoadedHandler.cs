using Eleon.Modding;
using ESB.Messaging;
using ESB.Models;
using Newtonsoft.Json.Linq;
//using ESB.Database;
//using System.Data.SQLite;

namespace ESB.LegacyEventHandlers
{
    public class LegacyPlayfieldLoadedHandler : ESB.HandlerBase, ILegacyPlayfieldLoadedHandler
    {
        public LegacyPlayfieldLoadedHandler(ContextData context) : base(context) { }

        public async void Handle(PlayfieldLoad obj)
        {
            await Execute(async () =>
            {
                // Database code removed - this was test code
                var json = new JObject();
                //var dbAccess = new DbAccess("Data Source=C:\\SteamRoot\\steamapps\\common\\Empyrion - Galactic Survival\\Saves\\Games\\Wanderlust\\global.db;Version=3;", false);
                //if (obj.playfield.Contains("[Sun"))
                //{
                //    dbAccess.JsonDataset(json, "Sector", "SELECT s.ssid, s.startype, s.name, s.sectorx, s.sectory, s.sectorz FROM SolarSystems s INNER JOIN Playfields p on p.ssid = s.ssid WHERE p.name = @playfieldName", new SQLiteParameter("@playfieldName", obj.playfield));
                //    if (json["Sector"] is JArray rows && rows.Count > 0)
                //    {
                //        dbAccess.JsonDataset(json, "Playfield", "SELECT * FROM Playfields WHERE ssid = @ssid", new SQLiteParameter("@ssid", Convert.ToInt32(rows[0]["ssid"].ToString())));
                //    }
                //    await _ctx.Messenger.SendAsync(MessageClass.Event, "PlayfieldLoaded", json.ToString()); //Newtonsoft.Json.Formatting.None
                //}
                await _ctx.Messenger.SendAsync(MessageClass.Event, "PlayfieldLoaded", json.ToString());
            });
        }
    }
}
