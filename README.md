# Blatmetro Browser

**Текущая версия: 0.4.4** · **Windows x64** · **Chromium**

Blatmetro Browser — настольный браузер, созданный **Blatmetro**. В разработке использовалась помощь ИИ **ChatGPT (OpenAI)**: обсуждение архитектуры, поиск и исправление ошибок, проверка кода и подготовка документации.

## Скачать

**Blatmetro Browser 0.4.4 уже выпущен — установщик Windows x64 доступен для скачивания.**

[Скачать BlatmetroBrowser-Setup-0.4.4-x64.exe](https://github.com/Blatmetro/Blatmetro-Browser/releases/download/v0.4.4/BlatmetroBrowser-Setup-0.4.4-x64.exe)

[Посмотреть все релизы](https://github.com/Blatmetro/Blatmetro-Browser/releases)

Установщик включает браузер и необходимые компоненты Chromium.

## Возможности версии 0.4.4

- Встроенный Chromium на базе CefSharp.
- Несколько вкладок: создание, переключение и закрытие.
- Адресная строка для URL и поисковых запросов.
- Выбор поисковой системы: DuckDuckGo, Google, Microsoft Bing, Brave Search, Яндекс, Ecosia, Qwant, Startpage, Yahoo, PrivacyWall, Perplexity и Mail.ru.
- Кнопки «Назад», «Вперёд», «Обновить» и переход на домашнюю страницу.
- Закладки с локальным хранением в JSON.
- Тёмный интерфейс в духе Blatmetro Flow.
- Разворачивание окна и полноэкранный режим.
- Горячая клавиша `F11` для полноэкранного режима и `Esc` для выхода из него.
- Поиск со страницы новой вкладки через выбранную поисковую систему.

Это ранний выпуск. История посещений, синхронизация, магазин расширений, автоматическое обновление и полноценная панель настроек пока не реализованы. Поведение браузера с точки зрения приватности ещё требует сетевого тестирования: отключение отдельных фоновых функций само по себе не доказывает полного отсутствия телеметрии или других сетевых запросов.

## Системные требования

- Windows 10/11 x64.
- Подключение к интернету для просмотра сайтов и поиска.

Браузер включает Chromium, поэтому размер установки и потребление оперативной памяти выше, чем у небольших нативных приложений. На компьютерах с 4 ГБ ОЗУ производительность может быть ограничена.

## Установка

1. Скачайте `BlatmetroBrowser-Setup-0.4.4-x64.exe` со [страницы последнего релиза](https://github.com/Blatmetro/Blatmetro-Browser/releases/latest).
2. Запустите установщик.
3. Следуйте инструкциям мастера установки. Папка по умолчанию: `C:\Program Files\Blatmetro Browser`.
4. Запустите Blatmetro Browser из меню «Пуск» или с ярлыка на рабочем столе, если он был создан.

## Сборка из исходников (Windows)

Требования: .NET 10 SDK и подключение к интернету для восстановления NuGet-пакетов.

Клонирование репозитория:

```powershell
git clone https://github.com/Blatmetro/Blatmetro-Browser.git
cd Blatmetro-Browser
```

Восстановление пакетов и запуск:

```powershell
dotnet restore .\BlatmetroBrowser.csproj -r win-x64
dotnet run --project .\BlatmetroBrowser.csproj -c Release
```

Публикация автономной сборки Windows x64:

```powershell
dotnet publish .\BlatmetroBrowser.csproj -c Release -r win-x64 --self-contained true
```

Скрипт установщика Inno Setup: `BlatmetroBrowserSetup.iss`. Компилируйте его после публикации приложения.

## Пользовательские данные

Профиль Chromium и кэш хранятся в `%LOCALAPPDATA%\Blatmetro\Browser`. Закладки и настройки находятся в `%APPDATA%\Blatmetro\Browser`.

## Технологии

- C#
- WPF
- .NET 10
- CefSharp
- Chromium

## Сторонние компоненты и лицензии

Blatmetro Browser использует CefSharp и Chromium Embedded Framework (CEF), основанный на Chromium. См. [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). В распространяемые сборки необходимо включать лицензионные тексты и уведомления, требуемые точными версиями CefSharp, CEF, Chromium и других поставляемых сторонних компонентов.

- [CefSharp](https://github.com/cefsharp/CefSharp)
- [Chromium Embedded Framework](https://github.com/chromiumembedded/cef)
- [Пакет CefSharp NuGet](https://www.nuget.org/packages/CefSharp.Wpf.NETCore/154.0.340)

## Версия для Linux

Linux-версия с кроссплатформенным интерфейсом и распространением через Flatpak находится в разработке. Пока она недоступна для скачивания. Текущий релиз 0.4.4 предназначен для Windows x64.

## Благодарности

**Создатель проекта и ведущий разработчик:** [Blatmetro](https://github.com/Blatmetro)

**Помощь ИИ:** ChatGPT (OpenAI) — помощь в обсуждении разработки, отладке, проверке кода и подготовке документации.

Проект Blatmetro Browser создаётся и сопровождается Blatmetro. Использование ИИ-помощи не заменяет автора или сопровождающего проекта.
