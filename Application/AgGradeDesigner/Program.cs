using AgGradeDesigner.Diagnostics;

using Avalonia;

namespace AgGradeDesigner
{
    /// <summary>
    /// Application entry point and Avalonia host configuration.
    /// </summary>
    static internal class Program
    {
        /// <summary>
        /// Starts the desktop application with the classic lifetime.
        /// </summary>
        /// <param name="Args">Command-line arguments passed to the host.</param>
        [STAThread]
        static public void Main
            (
            string[] Args
            )
        {
            AppLaunchOptions.Parse(Args);
            ProgressiveThreeDDiagnostics.Configure(AppLaunchOptions.DebugLog);
            ViewerLifecycleDiagnostics.Configure(AppLaunchOptions.DebugLog);
            if (AppLaunchOptions.DebugLog)
                ImportDiagnosticsLog.SetEnabled(true, "CommandLine");

            // Build and run the Avalonia app on the UI thread entry point.
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(Args);
        }

        /// <summary>
        /// Configures the Avalonia application builder for platform detection and logging.
        /// </summary>
        /// <returns>Configured builder ready for lifetime startup.</returns>
        static public AppBuilder BuildAvaloniaApp
            (
            )
        {
            // Apply platform defaults, fonts, and trace logging before host start.
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .WithInterFont()
                .LogToTrace();
        }
    }
}
