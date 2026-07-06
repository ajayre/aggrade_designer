using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Services
{
    /// <summary>
    /// Application-scoped access to persisted user settings outside project files.
    /// </summary>
    public sealed class UserSettingsService
    {
        /// <summary>Backing store for persisted user settings.</summary>
        private readonly UserSettingsStore _Store;

        /// <summary>In-memory AI settings used for live chat requests.</summary>
        private AiSettings _ActiveAiSettings;

        /// <summary>In-memory display unit settings used for UI formatting.</summary>
        private DisplayUnitsSettings _ActiveDisplayUnitsSettings;

        /// <summary>In-memory haul settings used for generate-haul runs.</summary>
        private HaulSettings _ActiveHaulSettings;

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
            _ActiveDisplayUnitsSettings = _Store.LoadDisplayUnitsSettings();
            _ActiveHaulSettings = _Store.LoadHaulSettings();
        }

        /// <summary>
        /// Currently active AI settings used for chat requests.
        /// </summary>
        public AiSettings ActiveAiSettings => _ActiveAiSettings;

        /// <summary>
        /// Currently active display unit settings used for UI formatting.
        /// </summary>
        public DisplayUnitsSettings ActiveDisplayUnitsSettings => _ActiveDisplayUnitsSettings;

        /// <summary>
        /// Currently active haul settings used for generate-haul runs.
        /// </summary>
        public HaulSettings ActiveHaulSettings => _ActiveHaulSettings;

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
        /// Loads a copy of the persisted display unit settings for dialog editing.
        /// </summary>
        /// <returns>Settings snapshot from disk.</returns>
        public DisplayUnitsSettings LoadDisplayUnitsSettingsForDialog
            (
            )
        {
            return _Store.LoadDisplayUnitsSettings();
        }

        /// <summary>
        /// Persists display unit settings and updates the active in-memory copy.
        /// </summary>
        /// <param name="Settings">Accepted display unit settings.</param>
        public void SaveDisplayUnitsSettings
            (
            DisplayUnitsSettings Settings
            )
        {
            _Store.SaveDisplayUnitsSettings(Settings);
            _ActiveDisplayUnitsSettings = Settings.Clone();
        }

        /// <summary>
        /// Loads a copy of the persisted haul settings for dialog editing.
        /// </summary>
        /// <returns>Settings snapshot from disk.</returns>
        public HaulSettings LoadHaulSettingsForDialog
            (
            )
        {
            return _Store.LoadHaulSettings();
        }

        /// <summary>
        /// Persists haul settings and updates the active in-memory copy.
        /// </summary>
        /// <param name="Settings">Accepted haul settings.</param>
        public void SaveHaulSettings
            (
            HaulSettings Settings
            )
        {
            _Store.SaveHaulSettings(Settings);
            _ActiveHaulSettings = Settings.Clone();
        }

        /// <summary>
        /// Absolute path to the on-disk user settings file.
        /// </summary>
        public string SettingsFilePath => _Store.SettingsFilePath;
    }
}
