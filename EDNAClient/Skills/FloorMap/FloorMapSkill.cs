using System.Collections.Generic;
using System.Threading.Tasks;
using EDNAClient.Core;
using ESB.Messaging;

namespace EDNAClient.Skills.FloorMap
{
    public class FloorMapSkill : IEdnaSkill, IHotkeyProvider
    {
        private readonly FloorMapViewModel _viewModel;
        private readonly FloorMapWindow    _window;
        private FloorMapper? _mapper;

        public string Id => "FloorMap";

        public FloorMapSkill(FloorMapViewModel viewModel)
        {
            _viewModel = viewModel;
            _window    = new FloorMapWindow(viewModel);
        }

        public async Task StartAsync(IMessenger messenger)
        {
            _mapper = new FloorMapper(messenger, _viewModel);
            _window.SetMapper(_mapper);
            await _mapper.StartAsync();
            _window.Show();
        }

        public void Stop()
        {
            _mapper?.Stop();
            _mapper = null;
            if (_window.IsVisible) _window.Hide();
        }

        public void SnapToGameWindow()
        {
            if (_window.IsVisible) _window.SnapToGameWindow();
        }

        public IEnumerable<HotkeyRequest> GetHotkeyRequests()
        {
            // Ctrl+Shift+R — refresh floor map data on demand
            yield return new HotkeyRequest(
                HotkeyRequest.ModControl | HotkeyRequest.ModShift | HotkeyRequest.NoRepeat,
                0x52,   // VK_R
                () =>
                {
                    if (_mapper == null) return;
                    _viewModel.StatusText = "Scanning...";
                    _viewModel.IsLoading  = true;
                    _ = _mapper.RefreshAsync();
                });
        }
    }
}
