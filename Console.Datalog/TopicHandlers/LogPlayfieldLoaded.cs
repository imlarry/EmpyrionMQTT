using Newtonsoft.Json.Linq;
using System.Data.SQLite;
using ESBLog.Common;

namespace ESBLog.TopicHandlers
{
    public class LogPlayfieldLoaded
    {
        //private readonly LoggerSpecificContext _ctx;

        //public LogPlayfieldLoaded(LoggerSpecificContext ctx)
        //{
        //    _ctx = ctx;
        //}

        public async Task Handle(string topic, string payload)
        {
            await LogPlayfieldLoadedX(topic, payload);
        }

        private async Task LogPlayfieldLoadedX(string topic, string payload)
        {
            var x = topic + payload;
            //JObject PlayfieldEvent = JObject.Parse(payload);

            //// if playfield is in db return
            //var pft = GetOrInsertPlayfieldType(PlayfieldEvent["PlayfieldType"].ToString());
            //var pt = GetOrInsertPlanetType(PlayfieldEvent["PlanetType"].ToString());
            //var mt = GetOrInsertMapType(PlayfieldEvent["MapType"].ToString());
            //var st = GetOrInsertStarType(PlayfieldEvent["StarType"].ToString());
            //var sid = GetUpdateOrInsertSector(st, PlayfieldEvent["SectorName"].ToString(), Convert.ToInt32(PlayfieldEvent["SectorX"]), Convert.ToInt32(PlayfieldEvent["SectorY"]), Convert.ToInt32(PlayfieldEvent["SectorZ"]));
            await Task.CompletedTask;   // kludgy way to get rid of compiler warning
        }
        /*
        private int GetOrInsertStarType(string name)
        {
            int stid = 0;
            _ctx.DBconnection?.DoWork(db =>
            {
                using var command = new SQLiteCommand("SELECT id FROM StarType WHERE name = @name", db);
                command.Parameters.AddWithValue("@name", name);
                var result = command.ExecuteScalar();

                if (result != null)
                {
                    stid = Convert.ToInt32(result);
                }
                else
                {
                    using var insertCommand = new SQLiteCommand("INSERT INTO StarType (name) VALUES (@name); SELECT last_insert_rowid();", db);
                    insertCommand.Parameters.AddWithValue("@name", name);
                    stid = Convert.ToInt32(insertCommand.ExecuteScalar());
                }
            });

            return stid;
        }

        private int GetUpdateOrInsertSector(int stid, string name, int sectorx, int sectory, int sectorz)
        {
            int sectorId = 0;
            _ctx.DBconnection?.DoWork(db =>
            {
                using var selectCommand = new SQLiteCommand("SELECT id, name FROM Sector WHERE sectorx = @sectorx AND sectory = @sectory AND sectorz = @sectorz", db);
                selectCommand.Parameters.AddWithValue("@sectorx", sectorx);
                selectCommand.Parameters.AddWithValue("@sectory", sectory);
                selectCommand.Parameters.AddWithValue("@sectorz", sectorz);
                var reader = selectCommand.ExecuteReader();

                if (reader.Read())
                {
                    sectorId = reader.GetInt32(0);
                    if (reader.IsDBNull(1))
                    {
                        using var updateCommand = new SQLiteCommand("UPDATE Sector SET name = @name, asoftick = @asoftick WHERE id = @id", db);
                        updateCommand.Parameters.AddWithValue("@name", name);
                        //updateCommand.Parameters.AddWithValue("@asoftick", _ctx.ModApi.Application.GameTicks);
                        updateCommand.Parameters.AddWithValue("@id", sectorId);
                        updateCommand.ExecuteNonQuery();
                    }
                }
                else
                {
                    using var insertCommand = new SQLiteCommand("INSERT INTO Sector (stid, name, sectorx, sectory, sectorz, asoftick) VALUES (@stid, @name, @sectorx, @sectory, @sectorz, @asoftick); SELECT last_insert_rowid();", db);
                    insertCommand.Parameters.AddWithValue("@stid", stid);
                    insertCommand.Parameters.AddWithValue("@name", name);
                    insertCommand.Parameters.AddWithValue("@sectorx", sectorx);
                    insertCommand.Parameters.AddWithValue("@sectory", sectory);
                    insertCommand.Parameters.AddWithValue("@sectorz", sectorz);
                   // insertCommand.Parameters.AddWithValue("@asoftick", _ctx.ModApi.Application.GameTicks);
                    sectorId = Convert.ToInt32(insertCommand.ExecuteScalar());
                }
            });

            return sectorId;
        }

        private int GetOrInsertPlayfieldType(string name)
        {
            int pftid = 0;
            _ctx.DBconnection?.DoWork(db =>
            {
                using var command = new SQLiteCommand("SELECT id FROM PlayfieldType WHERE name = @name", db);
                command.Parameters.AddWithValue("@name", name);
                var result = command.ExecuteScalar();

                if (result != null)
                {
                    pftid = Convert.ToInt32(result);
                }
                else
                {
                    using var insertCommand = new SQLiteCommand("INSERT INTO PlayfieldType (name) VALUES (@name); SELECT last_insert_rowid();", db);
                    insertCommand.Parameters.AddWithValue("@name", name);
                    pftid = Convert.ToInt32(insertCommand.ExecuteScalar());
                }
            });

            return pftid;
        }

        private int GetOrInsertPlanetType(string name)
        {
            int ptid = 0;
            _ctx.DBconnection?.DoWork(db =>
            {
                using var command = new SQLiteCommand("SELECT id FROM PlanetType WHERE name = @name", db);
                command.Parameters.AddWithValue("@name", name);
                var result = command.ExecuteScalar();

                if (result != null)
                {
                    ptid = Convert.ToInt32(result);
                }
                else
                {
                    using var insertCommand = new SQLiteCommand("INSERT INTO PlanetType (name) VALUES (@name); SELECT last_insert_rowid();", db);
                    insertCommand.Parameters.AddWithValue("@name", name);
                    ptid = Convert.ToInt32(insertCommand.ExecuteScalar());
                }
            });

            return ptid;
        }

        private int GetOrInsertMapType(string name)
        {
            int mtid = 0;
            _ctx.DBconnection?.DoWork(db =>
            {
                using var command = new SQLiteCommand("SELECT id FROM MapType WHERE name = @name", db);
                command.Parameters.AddWithValue("@name", name);
                var result = command.ExecuteScalar();

                if (result != null)
                {
                    mtid = Convert.ToInt32(result);
                }
                else
                {
                    using var insertCommand = new SQLiteCommand("INSERT INTO MapType (name) VALUES (@name); SELECT last_insert_rowid();", db);
                    insertCommand.Parameters.AddWithValue("@name", name);
                    mtid = Convert.ToInt32(insertCommand.ExecuteScalar());
                }
            });

            return mtid;
        }
        */
    }
}
