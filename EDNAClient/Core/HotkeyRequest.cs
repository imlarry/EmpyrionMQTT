using System;

namespace EDNAClient.Core
{
    /// <summary>
    /// Describes a global hotkey a skill wants registered.
    /// All EDNA hotkeys use Ctrl+Shift (ModControl | ModShift) to avoid
    /// conflicts with Empyrion, which has no three-modifier bindings.
    /// </summary>
    public sealed class HotkeyRequest
    {
        // RegisterHotKey fsModifiers flags
        public const uint ModControl = 0x0002;
        public const uint ModShift   = 0x0004;
        public const uint NoRepeat   = 0x4000;  // suppress repeat while key held

        public uint   Modifiers  { get; }
        public uint   VirtualKey { get; }
        public Action Callback   { get; }

        public HotkeyRequest(uint modifiers, uint virtualKey, Action callback)
        {
            Modifiers  = modifiers;
            VirtualKey = virtualKey;
            Callback   = callback ?? throw new ArgumentNullException(nameof(callback));
        }
    }
}
