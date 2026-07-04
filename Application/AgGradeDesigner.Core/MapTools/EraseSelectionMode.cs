namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Erase rectangle segment selection rule derived from drag direction (TOOLS-SPEC §3.3.6).
    /// </summary>
    public enum EraseSelectionMode
    {
        /// <summary>Top-left/right to bottom-left/right drag (TOOLS-SPEC §3.3.7).</summary>
        EnterAndCompletelyInside = 0,

        /// <summary>Bottom-left/right to top-left/right drag (TOOLS-SPEC §3.3.8).</summary>
        CompletelyInside = 1,
    }
}
