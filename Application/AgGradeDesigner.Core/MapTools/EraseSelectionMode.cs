namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Erase rectangle segment selection rule derived from drag direction (TOOLS-SPEC §3.3.6).
    /// </summary>
    public enum EraseSelectionMode
    {
        /// <summary>Drag down — enter-and-completely-inside rule (TOOLS-SPEC §3.3.7).</summary>
        EnterAndCompletelyInside = 0,

        /// <summary>Drag up — completely-inside rule (TOOLS-SPEC §3.3.8).</summary>
        CompletelyInside = 1,
    }
}
