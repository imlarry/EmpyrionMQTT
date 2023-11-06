using ESBMessaging;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;
using System.Data.SQLite;

namespace ESBlog;

// since the Logger uses a SQL connection in all handler routines so we derive an extended context for it
public class LoggerSpecificContext : BaseContextData
{
    public SQLiteConnection DBconnection { get; set; } = new("Data Source=Discovery.db");
}

public class Logger

{
    public LoggerSpecificContext CTX { get; set; } = new LoggerSpecificContext();

    readonly Messenger buslistener = new();

    public Logger()
    {
        CTX.Messenger = buslistener;
    }


    static void Main(string[] args)
    {
        Console.WriteLine("ESBlog: MQTT bus listener to SQLite event db");
        Console.WriteLine();
        if (args.Length != 0) { Console.WriteLine("...no support for params or switches yet"); }

        // create the non-static logger class
        Logger logger = new();
        logger.Init();

        // console loops while the logger works in background
        while (true)
        {
            Console.Write("ESBlog> ");
            string? cmd = Console.ReadLine()?.Trim();
            if (cmd != null && cmd.ToLower() == "exit") { return; }
            Console.WriteLine("the only command right now is 'exit'");
        }
    }

    async void Init()
    {
        // open db connection
        CTX.DBconnection.Open();

        // create messenger and configure
        await buslistener.ConnectAsync(CTX, "Logger", "localhost");

        // subscribe to events we want to log
        // ESB/Logger/c82bb816b42/Messenger.ProcessMessageAsync/I {"Topic":"ESB/Client/5a05bf426b3/ModApi.Application.OnPlayfieldLoaded/E","Exception":"No handler ModApi.Application.OnPlayfieldLoaded defined"}
        await buslistener.Subscribe("ESB/Client/+/ModApi.Application.OnPlayfieldLoaded/E", LogPlayfieldLoaded);
        //await buslistener.Subscribe("ESB/Client/+/ModApi.Playfield.EntityLoaded/E", LogEntityLoaded);
    }

    // ************************ subscription handler tasks ************************

    async void LogPlayfieldLoaded(string topic, string payload)
    {
        JObject PlayfieldEvent = JObject.Parse(payload);
        if (CTX.DBconnection is SQLiteConnection db)
        {
            using (var insertCommand = new SQLiteCommand("INSERT OR IGNORE INTO Playfield ( name, pftype, ptype, pclass, ssname, sectorx, sectory, sectorz, ispvp) VALUES (@name, @pftype, @ptype, @pclass, @ssname, @sectorx, @sectory, @sectorz, @ispvp)", db))
            {
                insertCommand.Parameters.AddWithValue("@name", PlayfieldEvent.GetValue("Name"));
                insertCommand.Parameters.AddWithValue("@pftype", PlayfieldEvent.GetValue("PlayfieldType"));
                insertCommand.Parameters.AddWithValue("@ptype", PlayfieldEvent.GetValue("PlanetType"));
                insertCommand.Parameters.AddWithValue("@pclass", PlayfieldEvent.GetValue("PlanetClass"));
                insertCommand.Parameters.AddWithValue("@ssname", PlayfieldEvent.GetValue("SolarSystemName"));
                var coordinates = PlayfieldEvent.GetValue("SolarSystemCoordinates")!.ToString().Split(' ')[1].Split('/');
                insertCommand.Parameters.AddWithValue("@sectorx", int.Parse(coordinates[0]));
                insertCommand.Parameters.AddWithValue("@sectory", int.Parse(coordinates[1]));
                insertCommand.Parameters.AddWithValue("@sectorz", int.Parse(coordinates[2]));
                insertCommand.Parameters.AddWithValue("@ispvp", PlayfieldEvent.GetValue("IsPvP"));

                insertCommand.ExecuteNonQuery();
            }
            await buslistener.SendAsync("LogPlayfieldLoaded", "Playfield Loaded");
        }
    }
    //async void LogEntityLoaded(string topic, string payload)
    //{
    //    JObject EntityEvent = JObject.Parse(payload);
    //    string txtQuery = string.Format("INSERT INTO EntityEventRaw (Id, Name, IsLocal, IsPoi, BelongsTo, DockedTo, Type) VALUES ( {0}, \"{1}\", {2}, {3}, {4}, {5}, {6} ); "
    //                        , EntityEvent.GetValue("Id")
    //                        , EntityEvent.GetValue("Name")
    //                        , EntityEvent.GetValue("IsLocal")
    //                        , EntityEvent.GetValue("IsPoi")
    //                        , EntityEvent.GetValue("BelongsTo")
    //                        , EntityEvent.GetValue("DockedTo")
    //                        , EntityEvent.GetValue("Type")
    //                        );
    //    await buslistener.SendAsync("LogEntityLoaded", txtQuery);
    //    if (CTX.DBconnection is SQLiteConnection db)
    //    {
    //        SQLiteCommand sql_cmd = db.CreateCommand();
    //        sql_cmd.CommandText = txtQuery;
    //        sql_cmd.ExecuteNonQuery();
    //    }
    //}
}
