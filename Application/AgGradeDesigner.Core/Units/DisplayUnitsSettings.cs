namespace AgGradeDesigner.Core.Units
{
    /// <summary>
    /// User-configurable display unit preferences persisted outside project files.
    /// </summary>
    public sealed class DisplayUnitsSettings
    {
        /// <summary>Selected display unit system for UI formatting.</summary>
        public DisplayUnitSystem System { get; set; } = DisplayUnitSystem.Imperial;

        /// <summary>
        /// Creates a deep copy suitable for dialog session editing.
        /// </summary>
        /// <returns>Independent settings snapshot.</returns>
        public DisplayUnitsSettings Clone
            (
            )
        {
            return new DisplayUnitsSettings
            {
                System = System
            };
        }

        /// <summary>
        /// Copies values from another settings instance into this one.
        /// </summary>
        /// <param name="Source">Settings to copy.</param>
        public void CopyFrom
            (
            DisplayUnitsSettings Source
            )
        {
            System = Source.System;
        }
    }
}
