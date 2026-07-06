using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Persistence
{
    /// <summary>
    /// Root document stored in the per-user application settings file.
    /// </summary>
    public sealed class UserSettingsDocument
    {
        /// <summary>Schema version for forward-compatible load/save.</summary>
        public int SchemaVersion { get; set; } = 2;

        /// <summary>AI provider configuration for chat features.</summary>
        public AiSettings Ai { get; set; } = new AiSettings();

        /// <summary>Display unit preferences for UI formatting.</summary>
        public DisplayUnitsSettings DisplayUnits { get; set; } = new DisplayUnitsSettings();

        /// <summary>Default haul-planning parameters for generate-haul runs.</summary>
        public HaulSettings Haul { get; set; } = new HaulSettings();
    }
}
