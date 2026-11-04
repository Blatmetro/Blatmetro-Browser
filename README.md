# Blatmetro Browser

**Версия:** 0.4.4 (рабочая версия с обновлённым выбором поисковика и тем)
**Платформа:** Windows x64
**Технологии:** C# / WPF / .NET 10 / CefSharp / Chromium

Blatmetro Browser — отдельный проект браузера для Windows. Позже его интерфейс и общую архитектуру можно будет адаптировать для BlatmetroOS, когда в ОС появится графическая подсистема и необходимые системные службы.

## Что уже есть в прототипе

- Встроенный Chromium через CefSharp (не системный WebView2).
- Несколько вкладок: создание, переключение и закрытие.
- Адресная строка: URL или поисковый запрос.
- Поиск через DuckDuckGo.
- Назад, вперёд, обновить и домашняя страница.
- Закладки с локальным сохранением в JSON.
- Горячие клавиши: `Ctrl+T`, `Ctrl+W`, `Ctrl+L`, `Ctrl+R`, `F5`, `Esc`.
- Тёмный интерфейс в духе Blatmetro Flow.
- Пара Chromium-флагов, отключающих некоторые необязательные фоновые сетевые функции.

Это ранний прототип. Пока не реализованы менеджер загрузок, история посещений, синхронизация, магазин расширений, обновлятор браузера и полноценная панель настроек. Параметры приватности ещё нужно проверять сетевыми тестами; отключение нескольких фоновых функций не доказывает полного отсутствия телеметрии или сетевых запросов.

## Требования

- Windows 10 x64 или новее.
- .NET 10 SDK (именно SDK, не только Runtime).
- Подключение к интернету для первого восстановления NuGet-пакетов.

Проект целится в Windows x64. Встроенный Chromium увеличивает размер приложения и потребление памяти. На компьютерах с 4 ГБ ОЗУ браузер может работать заметно тяжелее обычных небольших приложений.

## Запуск из исходников

1. Установите .NET 10 SDK.
2. Распакуйте папку проекта в путь без необычных символов, например `C:\Dev\BlatmetroBrowser`.
3. Откройте папку и запустите `run.cmd`.

Либо в терминале из папки проекта:

```powershell
dotnet restore BlatmetroBrowser.csproj -r win-x64
dotnet run --project BlatmetroBrowser.csproj -c Release
```

Для сборки:

```powershell
.\build.cmd
```

## Данные пользователя

Профиль Chromium и кэш хранятся в `%LOCALAPPDATA%\Blatmetro\Browser`. Закладки хранятся в `%APPDATA%\Blatmetro\Browser\bookmarks.json`.

## Сторонние компоненты и лицензии

Встроенный браузерный компонент предоставляется проектом CefSharp и основан на Chromium Embedded Framework (CEF) / Chromium. Перед распространением собранного приложения необходимо включить соответствующие лицензионные тексты и уведомления для CefSharp, CEF, Chromium и остальных распространяемых компонентов. В CEF доступна информация о лицензиях на страницах `about:license` и `about:credits`.

- CefSharp: https://github.com/cefsharp/CefSharp
- Chromium Embedded Framework: https://github.com/chromiumembedded/cef
- CefSharp NuGet: https://www.nuget.org/packages/CefSharp.Wpf.NETCore/154.0.340

## Примечание о сборке

Исходники подготовлены в среде без Windows/.NET SDK, поэтому здесь не выполнялась реальная компиляция и запуск WPF/CEF. Сборку нужно проверить на Windows с установленным .NET 10 SDK.


## Search engines

The address bar supports DuckDuckGo, Google, Microsoft Bing, Brave Search, Yandex, Ecosia, Qwant, Startpage, and Yahoo. Choose an engine from the selector on the right side of the toolbar. The selection is saved in `%APPDATA%\Blatmetro\Browser\settings.json`.


## Поисковые системы

В версии 0.3.0 доступны DuckDuckGo, Google, Microsoft Bing, Brave Search, Яндекс, Ecosia, Qwant, Startpage, Yahoo, PrivacyWall, Perplexity и @MAIL.RU. Выбор сохраняется в локальных настройках пользователя.


## Режимы окна (0.4.1)

- Кнопка `□` в правой части полосы вкладок разворачивает окно или восстанавливает прежний размер.
- Кнопка `⛶` включает полноэкранный режим без панелей браузера.
- `F11` включает/выключает полноэкранный режим.
- `Esc` выходит из полноэкранного режима; вне него останавливает загрузку страницы.


Исправление 0.4.1: доступ к строкам сетки и кнопкам режимов окна выполняется через runtime lookup, чтобы избежать ошибок генерации полей XAML.


## Исправление выбора поисковой системы (0.4.2)

Текущий выбор в списке поисковых систем считывается непосредственно перед поиском и сохраняется при закрытии списка. Это предотвращает использование устаревшего значения поисковика.


## Version 0.4.3 — new-tab search fix

The search field on the custom new-tab page now submits through a narrow CefSharp JavaScript bridge to the browser's `Navigate` method. It therefore uses the currently selected search engine from the toolbar instead of a hard-coded DuckDuckGo form action.


## Search submission fix (0.4.4)

The new-tab page submits search text using CefSharp.PostMessage. The WPF browser handles it through JavascriptMessageReceived and calls the common navigation/search routine, which uses the currently selected search engine. Search results load in the current tab.

## Unreleased working changes — search engine selector

The current working copy includes a redesigned search-engine selector with local icon assets, plus three appearance modes: System, Light, and Dark. System follows the Windows app-theme preference (`AppsUseLightTheme`) and reacts to Windows preference-change notifications. The mode is saved in the user settings.

The search engine icon PNGs are copied to the output and publish folders by the project file. See `Assets/SearchEngines/ASSETS-NOTICE.md` for icon-source and trademark notes.

This is an in-progress source update, not a published release. Build and test it on Windows before creating a new release. The WPF browser chrome and the bundled `Assets/NewTab.html` start page follow the selected mode. The browser also tells Chromium to emulate the selected `prefers-color-scheme` media feature, so sites that support automatic light/dark themes can switch with the browser. Pages that ignore this standard or have their own manually selected theme may not change. The browser does not invert or forcibly recolor arbitrary website content.


## Theme modes (working update)

The toolbar theme selector provides three modes:

- **Системная** — follows the Windows app appearance preference (`AppsUseLightTheme`) and updates when Windows reports a user-preference change.
- **Светлая** — uses a light browser interface, light new-tab page, and tells Chromium that `prefers-color-scheme` is `light`.
- **Тёмная** — uses a dark browser interface, dark new-tab page, and tells Chromium that `prefers-color-scheme` is `dark`.

The selected mode is stored in `%APPDATA%\Blatmetro\Browser\settings.json`. Existing `Light` and `Dark` settings from older builds remain valid. Website colors change only when the site supports the standard `prefers-color-scheme` feature; the browser does not apply a forced color inversion to pages.

## Один экземпляр приложения

Blatmetro Browser запускается в одном процессе, потому что все вкладки используют общий профиль Chromium в `%LOCALAPPDATA%\Blatmetro\Browser`. Повторный запуск не инициализирует второй экземпляр CEF и не открывает тот же профиль параллельно; вместо этого появляется сообщение о том, что браузер уже запущен. Для нескольких страниц используйте вкладки внутри браузера.

## Experimental Windows startup build

This test package includes the WPF SelfHost entry point for CefSharp:
- `Program.cs` dispatches CEF child processes before starting WPF or creating the single-instance mutex.
- `App.xaml.cs` initializes CefSharp with `performDependencyCheck: false` and writes a diagnostic log to `%LOCALAPPDATA%\Blatmetro\Browser\cef.log`.
- `disable-gpu` is enabled as a diagnostic compatibility fallback for older graphics drivers; this can disable WebGL and hardware-accelerated rendering.
- `do-not-de-elevate` is enabled to work around Chromium auto-de-elevation behavior reported by some Windows/CEF combinations.

This is a test source package, not a compiled release. Build and test it before considering distribution. User data remains outside the project under AppData.
