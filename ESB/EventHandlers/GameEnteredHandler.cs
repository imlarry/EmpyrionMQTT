using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class GameEnteredHandler : HandlerBase, IGameEnteredHandler
    {
        public GameEnteredHandler(ContextData context) : base(context) { }

        public async void Handle(bool hasEntered)
        {
            await Execute(async () =>
            {
                await _ctx.GameManager.StateChanged(hasEntered);
                JObject json = new JObject(
                    new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                    new JProperty("GameName", _ctx.GameManager.GameName),
                    new JProperty("GameIdentifier", _ctx.GameManager.GameIdentifier),
                    new JProperty("GameDataPath", _ctx.GameManager.GameDataPath),
                    new JProperty("SaveGamePath", _ctx.GameManager.SaveGamePath),
                    new JProperty("GameMode", _ctx.GameManager.GameMode));
                if (hasEntered)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Event, "Application.GameEnter", json.ToString(Newtonsoft.Json.Formatting.None));
                }
                else
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Event, "Application.GameExit", json.ToString(Newtonsoft.Json.Formatting.None));
                }
            });
        }
    }
}
