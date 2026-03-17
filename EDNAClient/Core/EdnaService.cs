using System.IO;
using System.Threading.Tasks;
using EDNAClient.ViewModels;
using ESB.Messaging;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Core
{
    public class EdnaService
    {
        private readonly EdnaContext _ctx;
        private readonly HudViewModel _viewModel;

        public EdnaService(HudViewModel viewModel)
        {
            _ctx = new EdnaContext();
            _viewModel = viewModel;
        }

        public async Task StartAsync()
        {
            await _ctx.Messenger.ConnectAsync(_ctx, "EDNA");
            await _ctx.Messenger.SubscribeEventAsync("+/E/Application.GameEnter/+/+", OnGameEnter);
            await _ctx.Messenger.SubscribeEventAsync("+/E/Application.GameExit/+/+", OnGameExit);
        }

        public async Task StopAsync()
        {
            await _ctx.Messenger.DisconnectAsync();
        }

        private async Task OnGameEnter(string topic, string payload)
        {
            var json = JObject.Parse(payload);
            _viewModel.GameName = json["GameName"]?.ToString();
            _viewModel.GameMode = json["GameMode"]?.ToString();

            var saveGamePath = json["SaveGamePath"]?.ToString();
            if (!string.IsNullOrEmpty(saveGamePath))
                await LoadEntityCountAsync(Path.Combine(saveGamePath, "global.db"));
        }

        private Task OnGameExit(string topic, string payload)
        {
            _viewModel.GameName = null;
            _viewModel.GameMode = null;
            _viewModel.EntityCount = null;
            return Task.CompletedTask;
        }

        private async Task LoadEntityCountAsync(string dbPath)
        {
            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                await using var connection = new SqliteConnection(cs);
                await connection.OpenAsync();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Entities";
                var count = (long)(await cmd.ExecuteScalarAsync())!;
                _viewModel.EntityCount = (int)count;
            }
            catch (Exception ex)
            {
                _viewModel.StatusDetail = $"global.db: {ex.Message}";
            }
        }
    }
}
