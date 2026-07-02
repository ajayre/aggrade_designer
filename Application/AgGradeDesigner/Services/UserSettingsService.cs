using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Persistence;

namespace AgGradeDesigner.Services
{
    /// <summary>
    /// Application-scoped access to persisted user settings outside project files.
    /// </summary>
    public sealed class UserSettingsService
    {
        private readonly UserSettingsStore _Store;
        private AiSettings _ActiveAiSettings;

        /// <summary>
        /// Shared user settings service for the running application instance.
        /// </summary>
        static public UserSettingsService Instance { get; } = new UserSettingsService();

        /// <summary>
        /// Creates the service using the default platform settings path.
        /// </summary>
        public UserSettingsService
            (
            )
            : this(new UserSettingsStore())
        {
        }

        /// <summary>
        /// Creates the service with a specific settings store (for tests).
        /// </summary>
        /// <param name="Store">Settings store implementation.</param>
        public UserSettingsService
            (
            UserSettingsStore Store
            )
        {
            _Store = Store;
            _ActiveAiSettings = _Store.LoadAiSettings();
        }

        /// <summary>
        /// Currently active AI settings used for chat requests.
        /// </summary>
        public AiSettings ActiveAiSettings => _ActiveAiSettings;

        /// <summary>
        /// Loads a copy of the persisted AI settings for dialog editing.
        /// </summary>
        /// <returns>Settings snapshot from disk.</returns>
        public AiSettings LoadAiSettingsForDialog
            (
            )
        {
            return _Store.LoadAiSettings();
        }

        /// <summary>
        /// Persists AI settings and updates the active in-memory copy.
        /// </summary>
        /// <param name="Settings">Accepted AI settings.</param>
        public void SaveAiSettings
            (
            AiSettings Settings
            )
        {
            _Store.SaveAiSettings(Settings);
            _ActiveAiSettings = Settings.Clone();
        }

        /// <summary>
        /// Absolute path to the on-disk user settings file.
        /// </summary>
        public string SettingsFilePath => _Store.SettingsFilePath;
    }
}
