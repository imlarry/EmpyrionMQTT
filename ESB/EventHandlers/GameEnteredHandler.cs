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
                // Order: publish GameEnter/GameExit on the CURRENT context BEFORE the Connect
                // retain change that triggers EDNA's swap. EDNA receives the event while still
                // subscribed to the old audience and processes it before its own SwitchContextAsync
                // drops that subscription. The Connect retain republish then carries
                // ContextRcId=destination so EDNA follows; the 500 ms settle inside Client's
                // SwitchContextAsync gives EDNA room to complete its swap before subsequent
                // publishes land on the new context.
                if (hasEntered)
                {
                    _ctx.GameManager.PrepareEnterGame();

                    var json = BuildPayload();
                    await _ctx.Bus.PublishContextEventAsync("App", "GameEnter", json);

                    await _ctx.GameManager.AnnounceConnectAsync(
                        _ctx.Bus.ContextRcId, _ctx.GameManager.GameRcId, true);
                    await _ctx.GameManager.EnterGame();
                }
                else
                {
                    var json = BuildPayload();
                    await _ctx.Bus.PublishContextEventAsync("App", "GameExit", json);

                    // Pre-republish the Lobby Connect retain with the cleared (post-exit) state
                    // BEFORE signaling on the game ctx. A peer that follows the game-side signal
                    // back to Lobby resubscribes there and the broker delivers the latest retain;
                    // if that retain still said "in-game" the peer would churn straight back to
                    // game and miss the GameExit. Sequential awaits ensure the lobby retain is
                    // updated on the broker before the game-side signal goes out.
                    await _ctx.GameManager.AnnounceConnectAsync(
                        _ctx.GameManager.LobbyRcId, _ctx.GameManager.LobbyRcId, false);
                    await _ctx.GameManager.AnnounceConnectAsync(
                        _ctx.Bus.ContextRcId, _ctx.GameManager.LobbyRcId, false);
                    await _ctx.GameManager.ExitGame();
                }
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "GameEntered", ex.ToString()); } catch { }
            }
        }

        private JObject BuildPayload()
        {
            return new JObject(
                new JProperty("GameTicks",    _ctx.ModApi.Application.GameTicks),
                new JProperty("GameName",     _ctx.GameManager.GameName),
                new JProperty("GameRcId",     _ctx.GameManager.GameRcId),
                new JProperty("SaveGamePath", _ctx.GameManager.SaveGamePath),
                new JProperty("GameMode",     _ctx.GameManager.GameMode));
        }
    }
}
