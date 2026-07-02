using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for <see cref="UserSettingsStore"/> AI settings persistence.
    /// </summary>
    public sealed class UserSettingsStoreTests
    {
        /// <summary>
        /// Verifies AI settings round-trip through the user settings file independently of projects.
        /// </summary>
        [Fact]
        public void SaveAndLoad_AiSettings_RoundTrips
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                AiSettings Original = new AiSettings
                {
                    Provider = AiProviderKind.OpenRouter,
                    ModelOverride = "test/model",
                    ApiKey = "secret-key",
                    CustomBaseUrl = "https://example.com/v1",
                    CustomModel = "custom-model"
                };
                Store.SaveAiSettings(Original);
                AiSettings Loaded = Store.LoadAiSettings();

                Assert.Equal(AiProviderKind.OpenRouter, Loaded.Provider);
                Assert.Equal("test/model", Loaded.ModelOverride);
                Assert.Equal("secret-key", Loaded.ApiKey);
                Assert.Equal("https://example.com/v1", Loaded.CustomBaseUrl);
                Assert.Equal("custom-model", Loaded.CustomModel);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies persisted Local LLM provider resolves to a Phase 1/2 provider on load.
        /// </summary>
        [Fact]
        public void Load_LocalLlmProvider_FallsBackToOpenAi
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveAiSettings(new AiSettings { Provider = AiProviderKind.LocalLlm });
                AiSettings Loaded = Store.LoadAiSettings();

                Assert.Equal(AiProviderKind.OpenAi, Loaded.Provider);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
