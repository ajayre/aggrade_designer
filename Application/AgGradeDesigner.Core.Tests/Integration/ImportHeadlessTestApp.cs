using Avalonia;
using Avalonia.Headless;

namespace AgGradeDesigner.Core.Tests.Integration
{
    /// <summary>
    /// Minimal Avalonia application used by headless import handoff tests.
    /// </summary>
    public sealed class ImportHeadlessTestApp : Application
    {
        /// <summary>
        /// Builds the headless Avalonia application for unit-test sessions.
        /// </summary>
        public static AppBuilder BuildAvaloniaApp
            (
            )
        {
            return AppBuilder.Configure<ImportHeadlessTestApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true
                });
        }
    }
}
