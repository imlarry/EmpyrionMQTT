using System.Collections.Generic;

namespace EDNAClient.Configuration
{
    public class EdnaInfo
    {
        public HashSet<string> EnabledSkillIds { get; set; } = new HashSet<string>();

        // When true, -DTL- level messages are written to the log file.
        // Edit EDNA_Info.yaml to enable; takes effect on next startup.
        public bool DetailEnabled { get; set; }
    }
}
