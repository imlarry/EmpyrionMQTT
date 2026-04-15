using System.Collections.Generic;

namespace ESB.Messaging.Configuration
{
    public class EdnaInfo
    {
        public HashSet<string> EnabledSkillIds { get; set; } = new HashSet<string>();

        // Workspace window position/size: "Left,Top,Width,Height" (written by EDNAClient only)
        public string WorkspaceBounds { get; set; }
    }
}
