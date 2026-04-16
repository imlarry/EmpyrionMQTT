using Eleon.Modding;
using System;

namespace ESB.GameApi
{
    /// <summary>
    /// Base class for ESB's game mod entry point. Bridges V1 (ModInterface/DediAPI) lifecycle
    /// callbacks into the Broker request/response system and exposes typed events for subclasses.
    /// </summary>
    public abstract partial class EmpyrionModBase : ModInterface
    {
        public Broker Broker { get; set; } = new Broker();

        protected LogLevel LogLevel { get => Broker.LogLevel; set => Broker.LogLevel = value; }

        public delegate void APIEventHandler(CmdId eventId, ushort seqNr, object data);
        public event APIEventHandler API_Message_Received;

        public delegate void ExitEventHandler();
        public event ExitEventHandler API_Exit;

        public delegate void UpdateHandler(ulong tick);
        public event UpdateHandler Update_Received;

        public abstract void Initialize(ModGameAPI dediAPI);

        public void Game_Start(ModGameAPI dediAPI)
        {
            if (dediAPI == null) return;
            try
            {
                Broker.api = dediAPI;
                Initialize(dediAPI);
            }
            catch (Exception error) { Log($"Game_Start: {error}", LogLevel.Error); }
        }

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            try
            {
                Broker.HandleGameEvent(eventId, seqNr, data);
                API_Message_Received?.Invoke(eventId, seqNr, data);
            }
            catch (Exception error)
            {
                Log($"Game_Event Exception: {eventId}/{seqNr}/{data?.ToString()} : {error}");
            }
        }

        public void Game_Exit()   => API_Exit?.Invoke();
        public void Game_Update() { if (Broker.api != null) Update_Received?.Invoke(Broker.api.Game_GetTickTime()); }

        public void Log(string msg)                   => Broker.Log(msg);
        public void Log(string msg, LogLevel level)   => Broker.Log(msg, level);
        public void Log(Func<string> msg)             => Broker.Log(msg);
        public void Log(Func<string> msg, LogLevel level) => Broker.Log(msg, level);
    }
}
