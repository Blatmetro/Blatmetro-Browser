using System.Threading;
using System.Windows;

namespace BlatmetroBrowser;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // CEF child processes (renderer, GPU, utility) must be dispatched before
        // WPF starts and before the single-instance mutex is created.
        var subprocessExitCode = CefSharp.BrowserSubprocess.SelfHost.Main(args);
        if (subprocessExitCode >= 0)
        {
            return subprocessExitCode;
        }

        // Only the main browser process is protected by the single-instance guard.
        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\BlatmetroBrowser.SingleInstance",
            createdNew: out var createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "Blatmetro Browser уже запущен. Продолжайте работу в открытом окне браузера.",
                "Blatmetro Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return 0;
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
