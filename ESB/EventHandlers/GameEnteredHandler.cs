using System;
using System.Threading.Tasks;
using ESB.Interfaces;
using ESB.Messaging;
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
                _ctx.GameManager.StateChanged(hasEntered);
                var json = new JObject(
                    new JProperty("GameTicks",    _ctx.ModApi.Application.GameTicks),
                    new JProperty("GameName",     _ctx.GameManager.GameName),
                    new JProperty("GameRcId",     _ctx.GameManager.GameRcId),
                    new JProperty("SaveGamePath", _ctx.GameManager.SaveGamePath),
                    new JProperty("GameMode",     _ctx.GameManager.GameMode));
                var operation = hasEntered ? "GameEnter" : "GameExit";
                // Broadcast so subscribers learn the new GameRcId; they can then SubscribeAsync(GameRcId).
                await _ctx.Bus.PublishEventAsync(RoutingContextId.BroadcastValue, "App", operation, json);

                if (hasEntered)
                    await _ctx.Bus.SubscribeAsync(_ctx.GameManager.GameRcId);
                else
                    await _ctx.Bus.UnsubscribeAsync(_ctx.GameManager.GameRcId);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "GameEntered", ex.ToString()); } catch { }
            }
        }
    }
}
