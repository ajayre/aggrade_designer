using AgGradeDesigner.Core.Ai;

namespace AgGradeDesigner.Core.Persistence
{
    /// <summary>
    /// Root document stored in the per-user application settings file.
    /// </summary>
    public sealed class UserSettingsDocument
    {
        /// <summary>Schema version for forward-compatible load/save.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>AI provider configuration for chat features.</summary>
        public AiSettings Ai { get; set; } = new AiSettings();
    }
}
