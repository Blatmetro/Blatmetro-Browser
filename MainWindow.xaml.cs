using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CefSharp;
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
    private string _selectedSearchEngine = DefaultSearchEngine;
    private bool _loadingSettings;
    private List<Bookmark> _bookmarks = [];
    private BrowserTab? _activeTab;
    private bool _isFullscreen;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private WindowState _previousWindowState;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(_appDataDirectory);
        _bookmarksFile = Path.Combine(_appDataDirectory, "bookmarks.json");
        _settingsFile = Path.Combine(_appDataDirectory, "settings.json");
        LoadBookmarks();
        LoadSettings();
        _loadingSettings = true;
        SearchEngineBox.ItemsSource = SearchEngines.Keys.ToList();
        SearchEngineBox.SelectedItem = _selectedSearchEngine;
        _loadingSettings = false;
        RefreshBookmarksBar();
        AddTab();
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

        // Receive search requests from the local new-tab page through CefSharp's
        // built-in PostMessage bridge. This avoids async JS-object binding races.
        browser.JavascriptMessageReceived += (_, args) =>
        {
            var query = args.Message?.ToString();
            if (!string.IsNullOrWhiteSpace(query))
            {
                Dispatcher.BeginInvoke(() => Navigate(query));
            }
        };

        var title = new TextBlock
        {
            Text = "Новая вкладка",
            Foreground = Brushes.White,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 150
        };

        var closeButton = new Button
        {
            Content = "×",
            Foreground = new SolidColorBrush(Color.FromRgb(166, 174, 192)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(5, 0, 5, 1),
            FontSize = 16,
            Cursor = Cursors.Hand,
            ToolTip = "Закрыть вкладку"
        };

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
            Background = new SolidColorBrush(Color.FromRgb(25, 30, 42)),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(12, 7, 6, 7),
            Margin = new Thickness(0, 0, 5, 0),
            MinWidth = 130,
            Cursor = Cursors.Hand
        };

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

    private bool IsNewTabAddress(string? address) =>
        !string.IsNullOrWhiteSpace(address) &&
        string.Equals(address, GetNewTabAddress(), StringComparison.OrdinalIgnoreCase);

    private string GetNewTabAddress()
    {
        var newTabPath = Path.Combine(AppContext.BaseDirectory, "Assets", "NewTab.html");
        return File.Exists(newTabPath)
            ? new Uri(newTabPath).AbsoluteUri
            : "https://duckduckgo.com/";
    }

    private void SelectTab(BrowserTab tab)
    {
        _activeTab = tab;

        foreach (var item in _tabs)
        {
            var isSelected = item == tab;
            item.Browser.Visibility = isSelected ? Visibility.Visible : Visibility.Hidden;
            item.Header.Background = isSelected
                ? new SolidColorBrush(Color.FromRgb(48, 59, 80))
                : new SolidColorBrush(Color.FromRgb(25, 30, 42));
        }

        AddressBox.Text = IsNewTabAddress(tab.Browser.Address) ? string.Empty : tab.Browser.Address ?? string.Empty;
        UpdateNavigationButtons();
        UpdateBookmarkButton();
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
            if (SearchEngineBox.SelectedItem is string selectedEngine &&
                SearchEngines.ContainsKey(selectedEngine))
            {
                _selectedSearchEngine = selectedEngine;
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
        if (_loadingSettings || SearchEngineBox.SelectedItem is not string selected ||
            !SearchEngines.ContainsKey(selected))
        {
            return;
        }

        _selectedSearchEngine = selected;
        SaveSettings();
    }

    private void SearchEngineBox_DropDownClosed(object sender, EventArgs e)
    {
        if (!_loadingSettings && SearchEngineBox.SelectedItem is string selected &&
            SearchEngines.ContainsKey(selected))
        {
            _selectedSearchEngine = selected;
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
            var json = JsonSerializer.Serialize(new BrowserSettings(_selectedSearchEngine),
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
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 158)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            });
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
    private void Reload_Click(object sender, RoutedEventArgs e) => _activeTab?.Browser.Reload();
    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is not null)
        {
            _activeTab.Browser.Address = GetNewTabAddress();
        }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => AddTab();

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
    }

    private sealed record Bookmark(string Title, string Url);
    private sealed record BrowserSettings(string SearchEngine);
}

