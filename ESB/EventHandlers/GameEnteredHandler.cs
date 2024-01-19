using ESB.Common;
using ESB.Messaging;
using ESB.Intefaces;
using Newtonsoft.Json.Linq;

namespace ESB
{
    public class GameEnteredHandler : IGameEnteredHandler
    {
        private readonly ContextData _cntxt;
        private string _GameState;

        public GameEnteredHandler(ContextData cntxt)
        {
            _cntxt = cntxt;
        }
        public async void Handle(bool hasEntered)
        {
            if (hasEntered)
            {
                _cntxt.GameManager.SetGameDirectory();
                if (_cntxt.GameManager.GameName == _cntxt.GameManager.GameIdentifier)
                {
                    _GameState = "InSP";    // determine coop
                } else 
                {
                    _GameState = "InMP"; 
                }
            } else 
            {
                _GameState = "InLobby";
            }
            JObject json = new JObject(
                new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
                new JProperty("GameName", _cntxt.GameManager.GameName),
                new JProperty("GameIdentifier", _cntxt.GameManager.GameIdentifier),
                new JProperty("HasEntered", hasEntered),
                new JProperty("GameState", _GameState));
            await _cntxt.Messenger.SendAsync(MessageClass.Event, "Application.GameEntered", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
