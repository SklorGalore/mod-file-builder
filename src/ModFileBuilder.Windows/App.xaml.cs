using System.Windows;
using Velopack;
using Velopack.Sources;

namespace ModFileBuilder.Windows;

public partial class App : Application
{
    private const string RepositoryUrl = "https://github.com/SklorGalore/mod-file-builder";

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepositoryUrl));
            if (!manager.IsInstalled) return;

            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;

            await manager.DownloadUpdatesAsync(update);
            var result = MessageBox.Show(
                MainWindow,
                string.Format("Version {0} is ready to install. Save your work, then restart now?", update.TargetFullRelease.Version),
                "Update available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
                manager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine("Velopack update check failed: " + ex);
        }
    }
}
