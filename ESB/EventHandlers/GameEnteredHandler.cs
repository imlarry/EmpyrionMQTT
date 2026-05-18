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
            if (!_ctx.IsReady)
            {
                // queue until BusManager.Init completes; drained on the next Update tick
                _ = Execute(() => HandleAsync(hasEntered));
                return;
            }

            await HandleAsync(hasEntered);
        }

        private async Task HandleAsync(bool hasEntered)
        {
            try
            {
                // Swap ContextRcId between Lobby and Game; GameManager handles the bus sub swap.
                if (hasEntered)
                    await _ctx.GameManager.EnterGame();
                else
                    await _ctx.GameManager.ExitGame();

                var json = new JObject(
                    new JProperty("GameTicks",    _ctx.ModApi.Application.GameTicks),
                    new JProperty("GameName",     _ctx.GameManager.GameName),
                    new JProperty("GameRcId",     _ctx.GameManager.GameRcId),
                    new JProperty("SaveGamePath", _ctx.GameManager.SaveGamePath),
                    new JProperty("GameMode",     _ctx.GameManager.GameMode));
                var operation = hasEntered ? "GameEnter" : "GameExit";
                // Lifecycle stays on Broadcast so every participant can learn the new GameRcId.
                await _ctx.Bus.PublishEventAsync(RoutingContextId.BroadcastValue, "App", operation, json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "GameEntered", ex.ToString()); } catch { }
            }
        }
    }
}
