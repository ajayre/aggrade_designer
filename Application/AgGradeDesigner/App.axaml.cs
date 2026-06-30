using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AgGradeDesigner
{
    /// <summary>
    /// Avalonia application bootstrap: XAML load and main window creation.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Loads application XAML resources.
        /// </summary>
        public override void Initialize
            (
            )
        {
            // Load compiled XAML into the application instance.
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Creates the main window when the desktop lifetime starts.
        /// </summary>
        public override void OnFrameworkInitializationCompleted
            (
            )
        {
            // Wire the primary window for classic desktop lifetime only.
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime Desktop)
            {
                Desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
