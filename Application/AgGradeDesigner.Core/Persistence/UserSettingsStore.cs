using System.Text.Json;
using System.Text.Json.Serialization;

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Persistence
{
    /// <summary>
    /// Loads and saves per-user application settings outside project files.
    /// </summary>
    public sealed class UserSettingsStore
    {
        /// <summary>Default settings file name within the application data folder.</summary>
        public const string SETTINGS_FILE_NAME = "user-settings.json";

        /// <summary>Product folder name under platform application data roots.</summary>
        public const string APPLICATION_FOLDER_NAME = "AgGrade Designer";

        /// <summary>Absolute path to the JSON settings file on disk.</summary>
        private readonly string _SettingsFilePath;

        /// <summary>JSON serializer options for stable user settings persistence.</summary>
        private readonly JsonSerializerOptions _JsonOptions;

        /// <summary>
        /// Creates a store targeting the default platform application data path.
        /// </summary>
        public UserSettingsStore
            (
            )
            : this(ResolveDefaultSettingsFilePath())
        {
        }

        /// <summary>
        /// Creates a store that reads and writes a specific settings file path.
        /// </summary>
        /// <param name="SettingsFilePath">Absolute path to the JSON settings file.</param>
        public UserSettingsStore
            (
            string SettingsFilePath
            )
        {
            _SettingsFilePath = SettingsFilePath;
            _JsonOptions = CreateJsonOptions();
        }

        /// <summary>
        /// Absolute path to the settings file used by this store instance.
        /// </summary>
        public string SettingsFilePath => _SettingsFilePath;

        /// <summary>
        /// Loads persisted settings, returning defaults when the file is missing.
        /// </summary>
        /// <returns>Loaded or default user settings document.</returns>
        public UserSettingsDocument Load
            (
            )
        {
            if (!File.Exists(_SettingsFilePath))
                return new UserSettingsDocument();

            string Json = File.ReadAllText(_SettingsFilePath);
            UserSettingsDocument? Document = JsonSerializer.Deserialize<UserSettingsDocument>(Json, _JsonOptions);
            if (Document == null)
                return new UserSettingsDocument();

            Document.Ai.Provider = Document.Ai.ResolvePhase12Provider();
            return Document;
        }

        /// <summary>
        /// Persists the supplied settings document to disk.
        /// </summary>
        /// <param name="Document">Settings to write.</param>
        public void Save
            (
            UserSettingsDocument Document
            )
        {
            string? DirectoryPath = Path.GetDirectoryName(_SettingsFilePath);
            if (!string.IsNullOrEmpty(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            string Json = JsonSerializer.Serialize(Document, _JsonOptions);
            File.WriteAllText(_SettingsFilePath, Json);
        }

        /// <summary>
        /// Loads only the AI settings section from disk.
        /// </summary>
        /// <returns>AI settings snapshot.</returns>
        public AiSettings LoadAiSettings
            (
            )
        {
            return Load().Ai.Clone();
        }

        /// <summary>
        /// Saves only the AI settings section, preserving other document fields when present.
        /// </summary>
        /// <param name="AiSettings">AI settings to persist.</param>
        public void SaveAiSettings
            (
            AiSettings AiSettings
            )
        {
            UserSettingsDocument Document = Load();
            Document.Ai.CopyFrom(AiSettings);
            Save(Document);
        }

        /// <summary>
        /// Loads only the display units section from disk.
        /// </summary>
        /// <returns>Display units settings snapshot.</returns>
        public DisplayUnitsSettings LoadDisplayUnitsSettings
            (
            )
        {
            return Load().DisplayUnits.Clone();
        }

        /// <summary>
        /// Saves only the display units section, preserving other document fields when present.
        /// </summary>
        /// <param name="DisplayUnitsSettings">Display units settings to persist.</param>
        public void SaveDisplayUnitsSettings
            (
            DisplayUnitsSettings DisplayUnitsSettings
            )
        {
            UserSettingsDocument Document = Load();
            Document.DisplayUnits.CopyFrom(DisplayUnitsSettings);
            Save(Document);
        }

        /// <summary>
        /// Loads only the haul settings section from disk.
        /// </summary>
        /// <returns>Haul settings snapshot.</returns>
        public HaulSettings LoadHaulSettings
            (
            )
        {
            return Load().Haul.Clone();
        }

        /// <summary>
        /// Saves only the haul settings section, preserving other document fields when present.
        /// </summary>
        /// <param name="HaulSettings">Haul settings to persist.</param>
        public void SaveHaulSettings
            (
            HaulSettings HaulSettings
            )
        {
            UserSettingsDocument Document = Load();
            Document.Haul.CopyFrom(HaulSettings);
            Save(Document);
        }

        /// <summary>
        /// Resolves the platform-standard per-user settings file path.
        /// </summary>
        /// <returns>Absolute path to <see cref="SETTINGS_FILE_NAME"/>.</returns>
        static public string ResolveDefaultSettingsFilePath
            (
            )
        {
            string Root = ResolveApplicationDataRoot();
            return Path.Combine(Root, SETTINGS_FILE_NAME);
        }

        /// <summary>
        /// Resolves the per-user application data directory for the current OS.
        /// </summary>
        /// <returns>Absolute directory path for application settings.</returns>
        static public string ResolveApplicationDataRoot
            (
            )
        {
            if (OperatingSystem.IsWindows())
            {
                string? AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(AppData, APPLICATION_FOLDER_NAME);
            }

            if (OperatingSystem.IsMacOS())
            {
                string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(Home, "Library", "Application Support", APPLICATION_FOLDER_NAME);
            }

            string? XdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(XdgConfig))
            {
                string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                XdgConfig = Path.Combine(Home, ".config");
            }

            return Path.Combine(XdgConfig, APPLICATION_FOLDER_NAME);
        }

        /// <summary>
        /// Creates JSON serializer options for stable user settings persistence.
        /// </summary>
        /// <returns>Configured serializer options.</returns>
        static private JsonSerializerOptions CreateJsonOptions
            (
            )
        {
            JsonSerializerOptions Options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };
            Options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return Options;
        }
    }
}
