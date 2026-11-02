using System.IO;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;

namespace BlatmetroBrowser;

public partial class App : Application
{
    private bool _cefInitialized;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Blatmetro",
            "Browser");

        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(Path.Combine(appData, "Cache"));

        var settings = new CefSettings
        {
            CachePath = Path.Combine(appData, "Cache"),
            LogSeverity = LogSeverity.Disable,
            Locale = "ru"
        };

        // Reduce optional Chromium background traffic. This is not a claim that
        // every network request from every website or component is eliminated.
        settings.CefCommandLineArgs.Add("disable-background-networking", "1");
        settings.CefCommandLineArgs.Add("disable-component-update", "1");
        settings.CefCommandLineArgs.Add("disable-sync", "1");
        settings.CefCommandLineArgs.Add("disable-domain-reliability", "1");
        settings.CefCommandLineArgs.Add("disable-breakpad", "1");
        settings.CefCommandLineArgs.Add("no-first-run", "1");

        try
        {
            _cefInitialized = Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось запустить Chromium: {ex.Message}",
                "Blatmetro Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        if (!_cefInitialized)
        {
            MessageBox.Show(
                "Не удалось запустить Chromium. Проверьте установку приложения и его файлы.",
                "Blatmetro Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_cefInitialized)
        {
            Cef.Shutdown();
        }

        base.OnExit(e);
    }
}
