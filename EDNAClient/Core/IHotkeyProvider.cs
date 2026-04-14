using System.Collections.Generic;

namespace EDNAClient.Core
{
    /// <summary>
    /// Implemented by skills that want global hotkeys registered while they are active.
    /// EdnaService checks for this interface at skill-start and registers the returned
    /// requests via HotkeyManager. Hotkeys are unregistered when the game exits.
    /// </summary>
    public interface IHotkeyProvider
    {
        IEnumerable<HotkeyRequest> GetHotkeyRequests();
    }
}
