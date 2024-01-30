using ESB.Common;
using ESB.Messaging;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class GameEnteredHandler : IGameEnteredHandler
    {
        private readonly ContextData _cntxt;

        public GameEnteredHandler(ContextData cntxt)
        {
            _cntxt = cntxt;
        }
        public async void Handle(bool hasEntered)
        {
            await _cntxt.GameManager.StateChanged(hasEntered);
            JObject json = new JObject(
                new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
                new JProperty("GameName", _cntxt.GameManager.GameName),
                new JProperty("GameIdentifier", _cntxt.GameManager.GameIdentifier),
                new JProperty("GameDataPath", _cntxt.GameManager.GameDataPath),
                new JProperty("GameMode", _cntxt.GameManager.GameMode));
            if (hasEntered)
            {
                await _cntxt.Messenger.SendAsync(MessageClass.Event, "Application.GameEnter", json.ToString(Newtonsoft.Json.Formatting.None));
            }
            else
            {
                await _cntxt.Messenger.SendAsync(MessageClass.Event, "Application.GameExit", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}
