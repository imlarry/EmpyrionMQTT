namespace EDNAClient.Core
{
    /// <summary>
    /// Implemented by skills that live inside the WorkspaceWindow as a docked tab
    /// rather than as a standalone floating window. WorkspaceWindow discovers all
    /// IDockableSkill implementations and adds a tab for each on StartAsync.
    /// ThreatRadar intentionally does NOT implement this -- it stays as a transparent overlay.
    /// </summary>
    public interface IDockableSkill : IEdnaSkill
    {
        /// <summary>Human-readable tab title shown in the DockingManager header.</summary>
        string Title { get; }

        /// <summary>
        /// Creates the UserControl that represents this skill's panel.
        /// Called by WorkspaceWindow.AddSkillTab() during StartAsync.
        /// Must be called on the WPF dispatcher thread.
        /// </summary>
        System.Windows.Controls.UserControl CreatePanel();
    }
}
