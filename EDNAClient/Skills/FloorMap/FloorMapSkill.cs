using System.Collections.Generic;
using System.Threading.Tasks;
using EDNAClient.Core;
using EDNAClient.Workspace;
using ESB.Messaging;

namespace EDNAClient.Skills.FloorMap
{
    public class FloorMapSkill : IDockableSkill, IHotkeyProvider
    {
        private readonly FloorMapViewModel _viewModel;
        private readonly WorkspaceWindow   _workspace;
        private FloorMapper?  _mapper;
        private FloorMapView? _panel;

        public string Id    => "FloorMap";
        public string Title => "Floor Map";

        public FloorMapSkill(FloorMapViewModel viewModel, WorkspaceWindow workspace)
        {
            _viewModel = viewModel;
            _workspace = workspace;
        }

        public System.Windows.Controls.UserControl CreatePanel()
            => _panel ??= new FloorMapView(_viewModel);

        public async Task StartAsync(IMessenger messenger)
        {
            _mapper = new FloorMapper(messenger, _viewModel);
            await _mapper.StartAsync();
        }

        public void Stop()
        {
            _mapper?.Stop();
            _mapper = null;
            _panel  = null;
            _workspace.RemoveDocument(Id);
        }

        public void SnapToGameWindow() { }

        public IEnumerable<HotkeyRequest> GetHotkeyRequests()
        {
            // Ctrl+Shift+R -- open floor map and refresh data
            yield return new HotkeyRequest(
                HotkeyRequest.ModControl | HotkeyRequest.ModShift | HotkeyRequest.NoRepeat,
                0x52,   // VK_R
                () =>
                {
                    _workspace.OpenDocument(Title, Id, CreatePanel());

                    if (_mapper == null) return;
                    _viewModel.StatusText = "Scanning...";
                    _viewModel.IsLoading  = true;
                    _ = _mapper.RefreshAsync();
                });
        }
    }
}
