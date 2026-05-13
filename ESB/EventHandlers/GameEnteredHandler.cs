using System;
using System.Threading.Tasks;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public class GameEnteredHandler : HandlerBase, IGameEnteredHandler
    {
        public GameEnteredHandler(ContextData context) : base(context) { }

        public async void Handle(bool hasEntered)
        {
            try
            {
                await _ctx.GameManager.StateChanged(hasEntered);
                var json = new JObject(
                    new JProperty("GameTicks",      _ctx.ModApi.Application.GameTicks),
                    new JProperty("GameName",       _ctx.GameManager.GameName),
                    new JProperty("GameIdentifier", _ctx.GameManager.GameIdentifier),
                    new JProperty("SaveGamePath",   _ctx.GameManager.SaveGamePath),
                    new JProperty("GameMode",       _ctx.GameManager.GameMode));
                var operation = hasEntered ? "GameEnter" : "GameExit";
                await _ctx.Bus.PublishEventAsync("App", operation, json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync("EventHandlers", "GameEntered", ex.ToString()); } catch { }
            }
        }
    }
}
