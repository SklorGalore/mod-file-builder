using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ModFileBuilder.Windows;

public partial class MainWindow : Window
{
    private Process? server;

    public MainWindow() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetAcrylicBackdrop();
        try
        {
            var port = GetAvailablePort();
            var address = $"http://127.0.0.1:{port}";
            StartServer(address);
            await WaitForServer(address, TimeSpan.FromSeconds(45));
            Browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            await Browser.EnsureCoreWebView2Async();
            Browser.Source = new Uri($"{address}/?desktop=1");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The desktop workspace could not start.\n\n{ex.Message}",
                "Case modification builder", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void StartServer(string address)
    {
        var directory = AppContext.BaseDirectory;
        var publishedServer = Path.Combine(directory, "ModFileBuilder.Web.exe");
        ProcessStartInfo startInfo;
        if (File.Exists(publishedServer))
        {
            startInfo = new ProcessStartInfo(publishedServer, $"--urls {address}")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            var project = FindWebProject();
            startInfo = new ProcessStartInfo("dotnet", $"run --project \"{project}\" --no-launch-profile --urls {address}")
            {
                WorkingDirectory = Path.GetDirectoryName(project)!,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = File.Exists(publishedServer) ? "Production" : "Development";
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        server = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not launch the local app service.");
        server.OutputDataReceived += (_, _) => { };
        server.ErrorDataReceived += (_, _) => { };
        server.BeginOutputReadLine();
        server.BeginErrorReadLine();
    }

    private static string FindWebProject()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "ModFileBuilder.Web", "ModFileBuilder.Web.csproj");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("ModFileBuilder.Web.exe was not found beside the desktop app.");
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private async Task WaitForServer(string address, TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (server?.HasExited != false)
                throw new InvalidOperationException("The local app service exited before it was ready.");
            try
            {
                using var response = await client.GetAsync(address);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("The local app service did not become ready in time.");
    }

    private void SetAcrylicBackdrop()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return;
        var handle = new WindowInteropHelper(this).Handle;
        var backdrop = 3; // DWMSBT_TRANSIENTWINDOW: Desktop Acrylic on Windows 11.
        _ = DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (server is null) return;
        try
        {
            if (!server.HasExited) server.Kill(entireProcessTree: true);
            server.Dispose();
        }
        catch (InvalidOperationException) { }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
