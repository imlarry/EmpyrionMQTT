using ESBMessaging;
using System.Data.SQLite;
using Newtonsoft.Json.Linq;

namespace ESBlog;

// since the Logger uses a SQL connection in all handler routines so we derive an extended context for it
public class LoggerSpecificContext : BaseContextData
{
    public SQLiteConnection DBconnection { get; set; } = new("Data Source=EventLog.db");
}

public class Logger

{
    public LoggerSpecificContext CTX { get; set; } = new LoggerSpecificContext();

    readonly Messenger buslistener = new();

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
        await buslistener.Subscribe("ESB/Client/ModApi.GameEvent.BiomeChanged/E", LogGameEvent);
        await buslistener.Subscribe("ESB/Client/ModApi.Playfield.EntityLoaded/E", LogEntityEvent);
    }

    // ************************ subscription handler tasks ************************

    async void LogGameEvent(string topic, string payload)
    {
        JObject GameEvent = JObject.Parse(payload);
        string txtQuery = string.Format("INSERT INTO GameEventRaw (type, arg1, arg2, arg3, arg4, arg5) VALUES ( \"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\" ); "
                            , GameEvent.GetValue("Type")
                            , GameEvent.GetValue("Arg1") ?? ""
                            , GameEvent.GetValue("Arg2") ?? ""
                            , GameEvent.GetValue("Arg3") ?? ""
                            , GameEvent.GetValue("Arg4") ?? ""
                            , GameEvent.GetValue("Arg5") ?? ""
                            );
        await buslistener.SendAsync("LogGameEvent", txtQuery);
        if (CTX.DBconnection is SQLiteConnection db)
        {
            SQLiteCommand sql_cmd = db.CreateCommand();
            sql_cmd.CommandText = txtQuery;
            sql_cmd.ExecuteNonQuery();
        }
    }
    async void LogEntityEvent(string topic, string payload)
    {
        JObject EntityEvent = JObject.Parse(payload);
        string txtQuery = string.Format("INSERT INTO EntityEventRaw (Id, Name, IsLocal, IsPoi, BelongsTo, DockedTo, Type) VALUES ( {0}, \"{1}\", {2}, {3}, {4}, {5}, {6} ); "
                            , EntityEvent.GetValue("Id")
                            , EntityEvent.GetValue("Name")
                            , EntityEvent.GetValue("IsLocal")
                            , EntityEvent.GetValue("IsPoi")
                            , EntityEvent.GetValue("BelongsTo")
                            , EntityEvent.GetValue("DockedTo")
                            , EntityEvent.GetValue("Type")
                            );
        await buslistener.SendAsync("LogEntityEvent", txtQuery);
        if (CTX.DBconnection is SQLiteConnection db)
        {
            SQLiteCommand sql_cmd = db.CreateCommand();
            sql_cmd.CommandText = txtQuery;
            sql_cmd.ExecuteNonQuery();
        }
    }
}
