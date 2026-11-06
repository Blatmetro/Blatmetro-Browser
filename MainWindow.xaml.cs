using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Win32;
using CefSharp;
using CefSharp.DevTools.Emulation;
using CefSharp.Wpf;

namespace BlatmetroBrowser;

public partial class MainWindow : Window
{
    private const string DefaultSearchEngine = "DuckDuckGo";
    private static readonly Dictionary<string, string> SearchEngines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DuckDuckGo"] = "https://duckduckgo.com/?q=",
        ["Google"] = "https://www.google.com/search?q=",
        ["Microsoft Bing"] = "https://www.bing.com/search?q=",
        ["Brave Search"] = "https://search.brave.com/search?q=",
        ["Яндекс"] = "https://yandex.ru/search/?text=",
        ["Ecosia"] = "https://www.ecosia.org/search?q=",
        ["Qwant"] = "https://www.qwant.com/?q=",
        ["Startpage"] = "https://www.startpage.com/sp/search?query=",
        ["Yahoo"] = "https://search.yahoo.com/search?p=",
        ["PrivacyWall"] = "https://www.privacywall.org/search/secure/?q=",
        ["Perplexity"] = "https://www.perplexity.ai/search?q=",
        ["@MAIL.RU"] = "https://go.mail.ru/search?q="
    };
    private readonly List<BrowserTab> _tabs = [];
    private readonly string _appDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Blatmetro",
        "Browser");
    private readonly string _bookmarksFile;
    private readonly string _settingsFile;
    private readonly List<SearchEngineOption> _searchEngineOptions;
    private string _selectedSearchEngine = DefaultSearchEngine;
    private string _selectedTheme = "System";
    private bool _loadingSettings;
    private List<Bookmark> _bookmarks = [];
    private BrowserTab? _activeTab;
    private readonly Dictionary<int, DownloadEntry> _downloads = [];
    private bool _isFullscreen;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private WindowState _previousWindowState;

    public MainWindow()
    {
        InitializeComponent();
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BlatmetroBrowser.png");
        if (File.Exists(logoPath))
        {
            var logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri(logoPath, UriKind.Absolute);
            logo.CacheOption = BitmapCacheOption.OnLoad;
            logo.EndInit();
            logo.Freeze();
            Icon = logo;
        }
        Directory.CreateDirectory(_appDataDirectory);
        _bookmarksFile = Path.Combine(_appDataDirectory, "bookmarks.json");
        _settingsFile = Path.Combine(_appDataDirectory, "settings.json");
        LoadBookmarks();
        LoadSettings();
        _searchEngineOptions = CreateSearchEngineOptions();
        _loadingSettings = true;
        SearchEngineBox.ItemsSource = _searchEngineOptions;
        SearchEngineBox.SelectedValue = _selectedSearchEngine;
        ThemeModeBox.SelectedValue = _selectedTheme;
        _loadingSettings = false;

        // Follow Windows app-theme changes only while the user has selected System.
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        ApplyTheme();
        RefreshBookmarksBar();
        AddTab();
    }

    private static readonly (string Name, string IconFile)[] SearchEngineCatalog =
    [
        ("DuckDuckGo", "duckduckgo.png"),
        ("Google", "google.png"),
        ("Microsoft Bing", "bing.png"),
        ("Brave Search", "brave.png"),
        ("Яндекс", "yandex.png"),
        ("Ecosia", "ecosia.png"),
        ("Qwant", "qwant.png"),
        ("Startpage", "startpage.png"),
        ("Yahoo", "yahoo.png"),
        ("PrivacyWall", "privacywall.png"),
        ("Perplexity", "perplexity.png"),
        ("@MAIL.RU", "mailru.png")
    ];

    private static List<SearchEngineOption> CreateSearchEngineOptions()
    {
        var options = new List<SearchEngineOption>(SearchEngineCatalog.Length);
        foreach (var (name, iconFile) in SearchEngineCatalog)
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "SearchEngines", iconFile);
            if (!File.Exists(iconPath))
            {
                throw new FileNotFoundException($"Не найден логотип поисковика: {iconFile}", iconPath);
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 48;
            bitmap.EndInit();
            bitmap.Freeze();
            options.Add(new SearchEngineOption(name, bitmap));
        }

        return options;
    }

    private static bool IsValidThemeMode(string? theme) =>
        theme is not null &&
        (theme.Equals("System", StringComparison.OrdinalIgnoreCase) ||
         theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ||
         theme.Equals("Dark", StringComparison.OrdinalIgnoreCase));

    private static bool IsWindowsAppThemeLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int lightTheme ? lightTheme != 0 : true;
        }
        catch (System.Security.SecurityException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private string GetEffectiveTheme()
    {
        if (_selectedTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            return IsWindowsAppThemeLight() ? "Light" : "Dark";
        }

        return _selectedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
    }

    private void ApplyTheme()
    {
        var effectiveTheme = GetEffectiveTheme();
        var isDark = effectiveTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        var palette = isDark
            ? new Dictionary<string, string>
            {
                ["WindowBackground"] = "#10131B", ["Surface"] = "#191E2A",
                ["SurfaceHover"] = "#252D3D", ["SurfaceActive"] = "#303B50",
                ["Accent"] = "#8B7CFF", ["TextPrimary"] = "#F3F4FA", ["TextSecondary"] = "#A6AEC0",
                ["TabStripBackground"] = "#0C0F16", ["TabActiveBackground"] = "#303B50",
                ["TabInactiveBackground"] = "#191E2A", ["BookmarkBarBackground"] = "#141925",
                ["BookmarkBorder"] = "#252D3D", ["AddressBackground"] = "#202737",
                ["AddressBorder"] = "#39445A", ["SearchControlBackground"] = "#202737",
                ["SearchControlBorder"] = "#39445A", ["SearchPopupBackground"] = "#171D2A",
                ["SearchPopupBorder"] = "#3A4560", ["SearchItemHoverBackground"] = "#252D3D",
                ["SearchItemSelectedBackground"] = "#332D5E", ["SearchIconTileBackground"] = "#FFFFFF",
                ["SearchFocusBorder"] = "#8B7CFF"
            }
            : new Dictionary<string, string>
            {
                ["WindowBackground"] = "#F5F7FB", ["Surface"] = "#FFFFFF",
                ["SurfaceHover"] = "#EEF1F7", ["SurfaceActive"] = "#E4E9F3",
                ["Accent"] = "#6757E8", ["TextPrimary"] = "#1B2333", ["TextSecondary"] = "#657087",
                ["TabStripBackground"] = "#E9EDF5", ["TabActiveBackground"] = "#FFFFFF",
                ["TabInactiveBackground"] = "#E6EAF2", ["BookmarkBarBackground"] = "#F7F8FC",
                ["BookmarkBorder"] = "#DCE2ED", ["AddressBackground"] = "#FFFFFF",
                ["AddressBorder"] = "#D5DCE8", ["SearchControlBackground"] = "#FFFFFF",
                ["SearchControlBorder"] = "#D4DCE8", ["SearchPopupBackground"] = "#FFFFFF",
                ["SearchPopupBorder"] = "#D9DFEA", ["SearchItemHoverBackground"] = "#F0F2F8",
                ["SearchItemSelectedBackground"] = "#ECE9FF", ["SearchIconTileBackground"] = "#F4F6FA",
                ["SearchFocusBorder"] = "#7462F4"
            };

        foreach (var (key, hex) in palette)
        {
            if (ColorConverter.ConvertFromString(hex) is Color color)
            {
                Application.Current.Resources[key] = new SolidColorBrush(color);
            }
        }

        ThemeModeBox.ToolTip = _selectedTheme switch
        {
            "System" => $"Тема: Системная ({(isDark ? "тёмная" : "светлая")})",
            "Light" => "Тема: Светлая",
            _ => "Тема: Тёмная"
        };

        foreach (var tab in _tabs)
        {
            tab.Header.SetResourceReference(Border.BackgroundProperty,
                tab == _activeTab ? "TabActiveBackground" : "TabInactiveBackground");

            // Tell Chromium which color scheme the user selected. Pages that support
            // prefers-color-scheme can update their own CSS without being force-recolored.
            _ = ApplyWebsiteColorSchemeAsync(tab.Browser, effectiveTheme);

            // The bundled start page has its own theme variables, so update it directly too.
            if (IsNewTabAddress(tab.Browser.Address))
            {
                try
                {
                    tab.Browser.ExecuteScriptAsync(
                        $"window.setBlatmetroTheme && window.setBlatmetroTheme('{effectiveTheme.ToLowerInvariant()}');");
                }
                catch (InvalidOperationException)
                {
                    // The initial URL also carries the selected theme, so first render is correct.
                }
            }
        }
    }

    private async Task ApplyWebsiteColorSchemeAsync(ChromiumWebBrowser browser, string? effectiveTheme = null)
    {
        if (!browser.IsBrowserInitialized)
        {
            return;
        }

        var colorScheme = (effectiveTheme ?? GetEffectiveTheme()).ToLowerInvariant();

        try
        {
            // CDP Emulation.setEmulatedMedia changes the live CSS media-query value.
            // This raises matchMedia('(prefers-color-scheme: ...)') changes in pages
            // that implement automatic light/dark appearance.
            var devTools = browser.GetDevToolsClient();
            await devTools.Emulation.SetEmulatedMediaAsync(
                media: string.Empty,
                features: new List<MediaFeature>
                {
                    new()
                    {
                        Name = "prefers-color-scheme",
                        Value = colorScheme
                    }
                });
        }
        catch (Exception ex)
        {
            // A page remains usable even if this CEF build cannot apply the emulation.
            System.Diagnostics.Debug.WriteLine($"Unable to apply website color scheme: {ex.Message}");
        }
    }

    private void ThemeModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || ThemeModeBox.SelectedValue is not string theme || !IsValidThemeMode(theme))
        {
            return;
        }

        _selectedTheme = theme;
        ApplyTheme();
        SaveSettings();
    }

    private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (!_selectedTheme.Equals("System", StringComparison.OrdinalIgnoreCase) ||
            Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_selectedTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                ApplyTheme();
            }
        });
    }

    private void AddTab(string? initialAddress = null)
    {
        var browser = new ChromiumWebBrowser
        {
            BrowserSettings =
            {
                DefaultEncoding = "UTF-8"
            }
        };
        browser.DownloadHandler = new BrowserDownloadHandler(this);
        browser.LifeSpanHandler = new PopupToTabHandler(this);

        // Apply the selected website color scheme as soon as this tab's CEF target exists.
        browser.IsBrowserInitializedChanged += (_, _) =>
        {
            if (browser.IsBrowserInitialized)
            {
                _ = ApplyWebsiteColorSchemeAsync(browser);
            }
        };

        // Receive search requests from the local new-tab page through CefSharp's
        // built-in PostMessage bridge. This avoids async JS-object binding races.
        browser.JavascriptMessageReceived += (_, args) =>
        {
            var query = args.Message?.ToString();
            if (!string.IsNullOrWhiteSpace(query))
            {
                Dispatcher.BeginInvoke(() => HandlePageMessage(query));
            }
        };

        var title = new TextBlock
        {
            Text = "Новая вкладка",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 150
        };

        title.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");

        var closeButton = new Button
        {
            Content = "×",

            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(5, 0, 5, 1),
            FontSize = 16,
            Cursor = Cursors.Hand,
            ToolTip = "Закрыть вкладку"
        };
        closeButton.SetResourceReference(Control.ForegroundProperty, "TextSecondary");

        var headerContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerContent.Children.Add(title);
        headerContent.Children.Add(closeButton);

        var header = new Border
        {
            Child = headerContent,
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(12, 7, 6, 7),
            Margin = new Thickness(0, 0, 5, 0),
            MinWidth = 130,
            Cursor = Cursors.Hand
        };
        header.SetResourceReference(Border.BackgroundProperty, "TabInactiveBackground");

        var tab = new BrowserTab(browser, header, title);
        _tabs.Add(tab);
        TabsStrip.Children.Add(header);
        BrowserHost.Children.Add(browser);

        header.MouseLeftButtonUp += (_, _) => SelectTab(tab);
        closeButton.Click += (_, _) => CloseTab(tab);

        browser.AddressChanged += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (_activeTab == tab)
            {
                var currentAddress = browser.Address;
                AddressBox.Text = IsNewTabAddress(currentAddress) ? string.Empty : currentAddress;
                UpdateSecurityIndicator(currentAddress);
                UpdateBookmarkButton();
            }
        });

        browser.TitleChanged += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            var currentTitle = browser.Title;
            var pageTitle = string.IsNullOrWhiteSpace(currentTitle) ? "Новая вкладка" : currentTitle;
            tab.Title.Text = pageTitle.Length > 24 ? pageTitle[..24] + "…" : pageTitle;
        });

        browser.LoadingStateChanged += (_, _) => Dispatcher.BeginInvoke(UpdateNavigationButtons);

        SelectTab(tab);
        browser.Address = string.IsNullOrWhiteSpace(initialAddress)
            ? GetNewTabAddress()
            : initialAddress;
    }

    private void HandlePageMessage(string message)
    {
        if (message.StartsWith("download:cancel:", StringComparison.Ordinal) && int.TryParse(message[16..], out var cancelId))
        {
            if (_downloads.TryGetValue(cancelId, out var download)) download.Callback?.Cancel();
            return;
        }
        if (message.StartsWith("download:open:", StringComparison.Ordinal) && int.TryParse(message[14..], out var openId))
        {
            if (_downloads.TryGetValue(openId, out var download) && download.Complete && File.Exists(download.Path))
                Process.Start(new ProcessStartInfo(download.Path) { UseShellExecute = true });
            return;
        }
        Navigate(message);
    }

    private bool IsNewTabAddress(string? address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var currentUri) || !currentUri.IsFile)
        {
            return false;
        }

        var newTabPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "NewTab.html"));
        return string.Equals(Path.GetFullPath(currentUri.LocalPath), newTabPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private string GetNewTabAddress()
    {
        var newTabPath = Path.Combine(AppContext.BaseDirectory, "Assets", "NewTab.html");
        return File.Exists(newTabPath)
            ? $"{new Uri(newTabPath).AbsoluteUri}?theme={GetEffectiveTheme().ToLowerInvariant()}"
            : "https://duckduckgo.com/";
    }

    private void SelectTab(BrowserTab tab)
    {
        _activeTab = tab;

        foreach (var item in _tabs)
        {
            var isSelected = item == tab;
            item.Browser.Visibility = isSelected ? Visibility.Visible : Visibility.Hidden;
            item.Header.SetResourceReference(Border.BackgroundProperty,
                isSelected ? "TabActiveBackground" : "TabInactiveBackground");
        }

        AddressBox.Text = IsNewTabAddress(tab.Browser.Address) ? string.Empty : tab.Browser.Address ?? string.Empty;
        UpdateSecurityIndicator(tab.Browser.Address);
        UpdateNavigationButtons();
        UpdateBookmarkButton();
        UpdateZoomLabel();
    }

    private void UpdateSecurityIndicator(string? address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            SecurityIndicator.Text = "🔒";
            SecurityIndicator.ToolTip = $"Защищённое HTTPS-соединение с {uri.Host}";
            SecurityIndicator.Foreground = Brushes.SeaGreen;
        }
        else if (Uri.TryCreate(address, UriKind.Absolute, out uri) && uri.Scheme is "http")
        {
            SecurityIndicator.Text = "⚠";
            SecurityIndicator.ToolTip = $"Незащищённое HTTP-соединение с {uri.Host}";
            SecurityIndicator.Foreground = Brushes.DarkOrange;
        }
        else
        {
            SecurityIndicator.Text = "⌕";
            SecurityIndicator.ToolTip = "Введите адрес сайта";
            SecurityIndicator.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
        }
    }

    private void CloseTab(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        _tabs.Remove(tab);
        TabsStrip.Children.Remove(tab.Header);
        BrowserHost.Children.Remove(tab.Browser);
        tab.Browser.Dispose();

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            AddTab();
            return;
        }

        if (_activeTab == tab)
        {
            SelectTab(_tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)]);
        }
    }

    private void Navigate(string input)
    {
        if (_activeTab is null)
        {
            return;
        }

        var value = input.Trim();
        if (value.Length == 0)
        {
            return;
        }

        string target;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri) &&
            absoluteUri.Scheme is "http" or "https" or "file" or "about")
        {
            target = absoluteUri.AbsoluteUri;
        }
        else if (!value.Contains(' ') &&
                 (value.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                  value.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase) ||
                  value.Contains('.')))
        {
            target = "https://" + value;
        }
        else
        {
            // Read the current ComboBox selection at the moment of searching.
            // This prevents a stale saved value from overriding the visible choice.
            if (SearchEngineBox.SelectedItem is SearchEngineOption selectedEngine &&
                SearchEngines.ContainsKey(selectedEngine.Name))
            {
                _selectedSearchEngine = selectedEngine.Name;
                SaveSettings();
            }

            var searchUrl = SearchEngines.TryGetValue(_selectedSearchEngine, out var configuredUrl)
                ? configuredUrl
                : SearchEngines[DefaultSearchEngine];
            target = searchUrl + Uri.EscapeDataString(value);
        }

        _activeTab.Browser.Address = target;
        AddressBox.Text = target;
    }

    private void UpdateNavigationButtons()
    {
        if (_activeTab is null)
        {
            BackButton.IsEnabled = false;
            ForwardButton.IsEnabled = false;
            return;
        }

        BackButton.IsEnabled = _activeTab.Browser.CanGoBack;
        ForwardButton.IsEnabled = _activeTab.Browser.CanGoForward;
    }

    private void UpdateBookmarkButton()
    {
        if (_activeTab is null || !Uri.TryCreate(_activeTab.Browser.Address, UriKind.Absolute, out var uri))
        {
            BookmarkButton.Content = "☆";
            return;
        }

        var exists = _bookmarks.Any(item => string.Equals(item.Url, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
        BookmarkButton.Content = exists ? "★" : "☆";
    }


    private void SearchEngineBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || SearchEngineBox.SelectedItem is not SearchEngineOption selected ||
            !SearchEngines.ContainsKey(selected.Name))
        {
            return;
        }

        _selectedSearchEngine = selected.Name;
        SaveSettings();
    }

    private void SearchEngineBox_DropDownClosed(object sender, EventArgs e)
    {
        if (!_loadingSettings && SearchEngineBox.SelectedItem is SearchEngineOption selected &&
            SearchEngines.ContainsKey(selected.Name))
        {
            _selectedSearchEngine = selected.Name;
            SaveSettings();
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsFile))
            {
                return;
            }

            var settings = JsonSerializer.Deserialize<BrowserSettings>(File.ReadAllText(_settingsFile));
            if (settings?.SearchEngine is string engine && SearchEngines.ContainsKey(engine))
            {
                _selectedSearchEngine = engine;
            }

            if (settings?.Theme is string theme && IsValidThemeMode(theme))
            {
                _selectedTheme = theme;
            }
        }
        catch (IOException)
        {
            _selectedSearchEngine = DefaultSearchEngine;
        }
        catch (JsonException)
        {
            _selectedSearchEngine = DefaultSearchEngine;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(new BrowserSettings(_selectedSearchEngine, _selectedTheme),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFile, json);
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Не удалось сохранить настройки: {ex.Message}", "Blatmetro Browser",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadBookmarks()
    {
        try
        {
            if (File.Exists(_bookmarksFile))
            {
                _bookmarks = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(_bookmarksFile)) ?? [];
            }
        }
        catch (IOException)
        {
            _bookmarks = [];
        }
        catch (JsonException)
        {
            _bookmarks = [];
        }
    }

    private void SaveBookmarks()
    {
        try
        {
            var json = JsonSerializer.Serialize(_bookmarks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_bookmarksFile, json);
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Не удалось сохранить закладки: {ex.Message}", "Blatmetro Browser",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshBookmarksBar()
    {
        BookmarksPanel.Children.Clear();

        if (_bookmarks.Count == 0)
        {
            BookmarksPanel.Children.Add(new TextBlock
            {
                Text = "Закладки появятся здесь",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            });
            ((TextBlock)BookmarksPanel.Children[0]).SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            return;
        }

        foreach (var bookmark in _bookmarks)
        {
            var button = new Button
            {
                Content = bookmark.Title,
                Tag = bookmark.Url,
                Style = (Style)FindResource("ToolbarButton"),
                FontSize = 11,
                Padding = new Thickness(9, 3, 9, 3),
                Margin = new Thickness(0, 0, 5, 0),
                MaxWidth = 180
            };
            button.Click += (_, _) =>
            {
                if (button.Tag is string url && _activeTab is not null)
                {
                    _activeTab.Browser.Address = url;
                }
            };
            BookmarksPanel.Children.Add(button);
        }
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        CloseMainMenu();
        if (_activeTab is null || !Uri.TryCreate(_activeTab.Browser.Address, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return;
        }

        var existing = _bookmarks.FirstOrDefault(item =>
            string.Equals(item.Url, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _bookmarks.Remove(existing);
        }
        else
        {
            var title = string.IsNullOrWhiteSpace(_activeTab.Title.Text) || _activeTab.Title.Text == "Новая вкладка"
                ? uri.Host
                : _activeTab.Title.Text;
            _bookmarks.Add(new Bookmark(title, uri.AbsoluteUri));
        }

        SaveBookmarks();
        RefreshBookmarksBar();
        UpdateBookmarkButton();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _activeTab?.Browser.Back();
    private void Forward_Click(object sender, RoutedEventArgs e) => _activeTab?.Browser.Forward();
    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        CloseMainMenu();
        _activeTab?.Browser.Reload();
    }
    private void Home_Click(object sender, RoutedEventArgs e)
    {
        CloseMainMenu();
        if (_activeTab is not null)
        {
            _activeTab.Browser.Address = GetNewTabAddress();
        }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        CloseMainMenu();
        AddTab();
    }

    private void MainMenuButton_Click(object sender, RoutedEventArgs e)
    {
        MainMenuPopup.IsOpen = !MainMenuPopup.IsOpen;
        if (MainMenuPopup.IsOpen)
        {
            UpdateZoomLabel();
        }
    }

    private void Downloads_Click(object sender, RoutedEventArgs e)
    {
        CloseMainMenu();
        var tab = _tabs.FirstOrDefault(t => t.IsDownloadsTab);
        if (tab is null)
        {
            AddTab("about:blank");
            tab = _activeTab!;
            tab.IsDownloadsTab = true;
            tab.Title.Text = "Загрузки";
            var downloadsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Downloads.html");
            tab.Browser.Address = File.Exists(downloadsPath) ? new Uri(downloadsPath).AbsoluteUri : "about:blank";
        }
        SelectTab(tab);
        RenderDownloads(tab);
    }

    private void RenderDownloads(BrowserTab? onlyTab = null)
    {
        foreach (var tab in onlyTab is null ? _tabs.Where(t => t.IsDownloadsTab) : [onlyTab])
        {
            if (!tab.Browser.IsBrowserInitialized || tab.Browser.IsLoading) continue;
            var json = JsonSerializer.Serialize(_downloads.Values.OrderByDescending(d => d.Id).Select(d => new { d.Id, name=d.Name, size=$"{FormatBytes(d.Received)} / {FormatBytes(d.Total)}", status=d.Status, progress=d.Progress, complete=d.Complete && File.Exists(d.Path), active=d.InProgress }));
            tab.Browser.ExecuteScriptAsync($"render({json});");
        }
    }

    private static string FormatBytes(long bytes) => bytes < 0 ? "Размер неизвестен" : bytes < 1024 ? $"{bytes} Б" : bytes < 1024 * 1024 ? $"{bytes / 1024d:0.#} КБ" : $"{bytes / (1024d * 1024):0.#} МБ";

    private void UpdateDownload(DownloadItem downloadItem, IDownloadItemCallback callback)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_downloads.TryGetValue(downloadItem.Id, out var item))
                _downloads[downloadItem.Id] = item = new DownloadEntry(downloadItem.Id);
            item.Name = downloadItem.SuggestedFileName;
            item.Path = downloadItem.FullPath;
            item.Received = downloadItem.ReceivedBytes;
            item.Total = downloadItem.TotalBytes;
            item.Progress = downloadItem.PercentComplete;
            item.Complete = downloadItem.IsComplete;
            item.InProgress = downloadItem.IsInProgress;
            item.Callback = callback;
            item.Status = downloadItem.IsComplete ? "Завершено" : downloadItem.IsInProgress ? "Загрузка" : "Прервано или ошибка";
            RenderDownloads();
        });
    }

    private sealed class DownloadEntry(int id)
    {
        public int Id { get; } = id;
        public string Name { get; set; } = "Загрузка";
        public string Path { get; set; } = "";
        public string Status { get; set; } = "Подготовка";
        public long Received { get; set; }
        public long Total { get; set; }
        public int Progress { get; set; }
        public bool Complete { get; set; }
        public bool InProgress { get; set; }
        public IDownloadItemCallback? Callback { get; set; }
    }

    private sealed class BrowserDownloadHandler(MainWindow owner) : CefSharp.Handler.DownloadHandler
    {
        protected override bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            if (!callback.IsDisposed)
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(directory);
                var name = Path.GetFileName(downloadItem.SuggestedFileName);
                callback.Continue(Path.Combine(directory, string.IsNullOrWhiteSpace(name) ? $"download-{downloadItem.Id}" : name), false);
            }
            return true;
        }

        protected override void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) => owner.UpdateDownload(downloadItem, callback);
    }

    private sealed class PopupToTabHandler(MainWindow owner) : CefSharp.Handler.LifeSpanHandler
    {
        protected override bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
            string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture,
            IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings,
            ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null!;
            var address = Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) &&
                          uri.Scheme is "http" or "https" or "about"
                ? targetUrl
                : "about:blank";
            owner.Dispatcher.BeginInvoke(() => owner.AddTab(address));
            return true;
        }
    }

    private void CloseMainMenu() => MainMenuPopup.IsOpen = false;

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ChangeZoom(-1);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ChangeZoom(1);

    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is not null)
        {
            _activeTab.Browser.ZoomLevel = 0;
        }
        UpdateZoomLabel();
    }

    private void ChangeZoom(double delta)
    {
        if (_activeTab is null)
        {
            return;
        }

        _activeTab.Browser.ZoomLevel = Math.Clamp(_activeTab.Browser.ZoomLevel + delta, -5, 6);
        UpdateZoomLabel();
    }

    private void UpdateZoomLabel()
    {
        var zoom = _activeTab is null
            ? 100
            : Math.Round(Math.Pow(1.2, _activeTab.Browser.ZoomLevel) * 100);
        ZoomResetButton.Content = $"{zoom:0}%";
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullscreen)
        {
            ExitFullscreen();
        }

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateWindowButtons();
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        CloseMainMenu();
        if (_isFullscreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        if (_isFullscreen)
        {
            return;
        }

        _previousWindowStyle = WindowStyle;
        _previousResizeMode = ResizeMode;
        _previousWindowState = WindowState;

        _isFullscreen = true;
        var rows = ((Grid)Content).RowDefinitions;
        rows[0].Height = new GridLength(0);
        rows[1].Height = new GridLength(0);
        rows[2].Height = new GridLength(0);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        if (FindName("FullscreenButton") is Button fullscreenButton)
        {
            fullscreenButton.Content = "⛶";
            fullscreenButton.ToolTip = "Выйти из полноэкранного режима (F11 или Esc)";
        }
    }

    private void ExitFullscreen()
    {
        if (!_isFullscreen)
        {
            return;
        }

        WindowState = WindowState.Normal;
        WindowStyle = _previousWindowStyle;
        ResizeMode = _previousResizeMode;
        var rows = ((Grid)Content).RowDefinitions;
        rows[0].Height = new GridLength(46);
        rows[1].Height = new GridLength(58);
        rows[2].Height = new GridLength(34);
        WindowState = _previousWindowState;

        _isFullscreen = false;
        if (FindName("FullscreenButton") is Button fullscreenButton)
        {
            fullscreenButton.ToolTip = "Полноэкранный режим (F11)";
        }
        UpdateWindowButtons();
    }

    private void UpdateWindowButtons()
    {
        if (FindName("MaximizeButton") is not Button maximizeButton)
        {
            return;
        }

        maximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        maximizeButton.ToolTip = WindowState == WindowState.Maximized
            ? "Восстановить окно"
            : "Развернуть окно";
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Navigate(AddressBox.Text);
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (ctrl && e.Key == Key.L)
        {
            AddressBox.Focus();
            AddressBox.SelectAll();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.T)
        {
            AddTab();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.W && _activeTab is not null)
        {
            CloseTab(_activeTab);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.R)
        {
            _activeTab?.Browser.Reload();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.Tab && _tabs.Count > 1)
        {
            var currentIndex = _tabs.IndexOf(_activeTab!);
            var reverse = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            var nextIndex = reverse
                ? (currentIndex - 1 + _tabs.Count) % _tabs.Count
                : (currentIndex + 1) % _tabs.Count;
            SelectTab(_tabs[nextIndex]);
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            _activeTab?.Browser.Reload();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            if (_isFullscreen)
            {
                ExitFullscreen();
            }
            else
            {
                EnterFullscreen();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_isFullscreen)
            {
                ExitFullscreen();
                e.Handled = true;
            }
            else
            {
                _activeTab?.Browser.Stop();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

        foreach (var tab in _tabs.ToArray())
        {
            tab.Browser.Dispose();
        }
        _tabs.Clear();
        base.OnClosed(e);
    }

    private sealed class BrowserTab(ChromiumWebBrowser browser, Border header, TextBlock title)
    {
        public ChromiumWebBrowser Browser { get; } = browser;
        public Border Header { get; } = header;
        public TextBlock Title { get; } = title;
        public bool IsDownloadsTab { get; set; }
    }

    private sealed record Bookmark(string Title, string Url);
    private sealed record BrowserSettings(string SearchEngine, string? Theme = null);
    private sealed record SearchEngineOption(string Name, ImageSource Icon);
}

