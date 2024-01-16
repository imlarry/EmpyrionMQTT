using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using ESBLog.Database;
using Newtonsoft.Json.Linq;
using System.Data.SQLite;
using System;

public class LegacyPlayfieldLoadedHandler
{
    private readonly ContextData _contextData;

    public LegacyPlayfieldLoadedHandler(ContextData contextData)
    {
        _contextData = contextData;
    }

    public async void Handle(PlayfieldLoad obj)
    {
        var json = new JObject();
        var dbAccess = new DbAccess("Data Source=C:\\SteamRoot\\steamapps\\common\\Empyrion - Galactic Survival\\Saves\\Games\\Wanderlust\\global.db;Version=3;", false);
        if (obj.playfield.Contains("[Sun"))
        {
            dbAccess.JsonDataset(json, "Sector", "SELECT s.ssid, s.startype, s.name, s.sectorx, s.sectory, s.sectorz FROM SolarSystems s INNER JOIN Playfields p on p.ssid = s.ssid WHERE p.name = @playfieldName", new SQLiteParameter("@playfieldName", obj.playfield));
            if (json["Sector"] is JArray rows && rows.Count > 0)
            {
                dbAccess.JsonDataset(json, "Playfield", "SELECT * FROM Playfields WHERE ssid = @ssid", new SQLiteParameter("@ssid", Convert.ToInt32(rows[0]["ssid"].ToString())));
            }
            await _contextData.Messenger.SendAsync(MessageClass.Event, "PlayfieldLoaded", json.ToString()); //Newtonsoft.Json.Formatting.None
        }
    }
}