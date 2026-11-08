# video2gif

Видео и картинки → GIF. Быстро, локально. Плюс скачивание видео и музыки.

**by DeepSeek v4.1 flash & arsikfox14 & dismasdismavich**

Установка не нужна: FFmpeg и yt-dlp находятся
внутри exe. Права администратора не требуются.

## Скриншоты

**(Версия 2.5 + TT + YOUTUBE И ТП + МУЗЫКА)**
<img width="967" height="603" alt="изображение" src="https://github.com/user-attachments/assets/4b65cabc-c135-4d55-973c-a39eda8c2220" />

**(Версия 2.0 + TikTok)**
<img width="990" height="620" alt="Jarvis" src="https://github.com/user-attachments/assets/1fefafc7-0939-407d-8c49-283a4217252b" />

**(Версия 2.0)**
<img width="950" height="602" alt="Friday" src="https://github.com/user-attachments/assets/7c5fe19e-5b17-486f-802b-321d1ab136b1" />

**(Версия 1.2)**
<img width="961" height="685" alt="video2gif 1.2" src="https://github.com/user-attachments/assets/5236780c-dbf5-4c52-a29a-dcbc58dc01f6" />

## Что нового в 2.4

- После окончания скачивания больше не появляется окно с сообщением —
  окно просто закрывается, файл уже лежит в выбранной папке.
- У формата WAV показан битрейт (`1411 кбит/с`), как у MP3 (`320 кбит/с`).

## Возможности

- Видео → GIF: MP4, MKV, AVI, MOV, WEBM, M4V, WMV, FLV, MPG, TS, 3GP, OGV.
- Картинки → GIF: PNG, JPG, WEBP, BMP, TIFF, TGA, AVIF.
- Качественный GIF через палитру: palettegen + paletteuse, два прохода.
- Sierra-сглаживание по умолчанию, 256 цветов.
- Частота кадров 1–60, по умолчанию 30.
- Начало и длительность фрагмента, ширина результата.
- Бесконечный цикл или один проигрыш.
- Быстрый режим — один проход вместо двух.
- Прогресс, отмена, предпросмотр, сохранение и открытие результата.
- Тёмная и светлая темы, тёмная по умолчанию.
- Перетаскивание файла в окно или на exe.
- Открытие файла, переданного аргументом командной строки.

## Скачивание

Меню **«Скачать»** в главном окне:

- **TikTok** — видео MP4 без водяного знака и фото-посты
- **YouTube и другие сайты** — YouTube, SoundCloud, VK и тысячи других
  сайтов через yt-dlp. На выбор видео MP4 или музыка MP3.
- **Музыка из Spotify** — ссылка на трек, альбом или плейлист.
  Программа читает открытые метаданные страницы Spotify, ищет трек
  на YouTube с проверкой длительности и сохраняет MP3 или WAV.
  Это не прямое скачивание из Spotify: DRM не обходится.

Все скачанные файлы и готовый GIF по умолчанию сохраняются
на **рабочий стол**. Папку можно изменить в окне сохранения.

## Как запустить

1. Скачать `Video2Gif.exe`
2. Двойной клик.
3. Перетащить видео или картинку в окно.

Требования: Windows 10/11, 64-бит, .NET Framework 4.8 (уже входит в Windows).
Интернет нужен только для скачивания.

При первом запуске FFmpeg и yt-dlp распаковываются в
`%LOCALAPPDATA%\Video2Gif`. Повторно распаковываться не будут.

## Сборка из исходников

Исходники лежат в корне репозитория (C#, WinForms, .NET Framework 4.8).
Компилятор берётся из Windows: `csc.exe`, отдельный SDK не нужен.

1. Положить в папку `bin` в корне проекта (рядом с `build.ps1`
   или с папкой `src`):

   - 64-битный `ffmpeg.exe` — https://ffmpeg.org (сборка gyan.dev);
   - `yt-dlp.exe` — https://github.com/yt-dlp/yt-dlp/releases/latest

2. Запустить:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Собирается `Video2gif.exe (jarvis)`.

Другие сборки:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -NoTikTok
powershell -ExecutionPolicy Bypass -File build.ps1 -Legacy
powershell -ExecutionPolicy Bypass -File build.ps1 -Legacy -NoTikTok
```

- `-NoTikTok` — Старая версия Video2gif.exe (friday) без кнопки и кода TikTok;
- `-Legacy` — старые имена Video2Gif.exe / Video2gif_notiktok.exe `(по факту нынешний но дипсик не правильно понял)`

Скрипт сжимает FFmpeg и yt-dlp в `build\*.gz`, рисует иконку и собирает exe
в корне проекта. Файлы `.cs` должны быть в UTF-8 с BOM.

Самопроверка без графического интерфейса:

```powershell
Jarvis.exe --selftest
```

Результат пишется в `%TEMP%\video2gif-selftest.log`.

## Файлы

- `Program.cs` — запуск, версия, имя сборки;
- `MainForm.cs`, `MainForm.UI.cs` — главное окно;
- `Theme.cs`, `Controls.cs` — тема и элементы интерфейса;
- `Core.cs` — FFmpeg, yt-dlp, конвертация;
- `MediaDownloader.cs` — загрузчик, Spotify, поиск на YouTube;
- `MediaDownloadForm.cs` — окно скачивания;
- `TikTok.cs`, `TikTokForm.cs` — скачивание TikTok;
- `SelfTest.cs` — самопроверка;
- `build.ps1` — сборка;
- `app.ico`, `app.manifest` — иконка и манифест.

## История версий

- **2.4\2.5** — без окна после скачивания; у WAV показан битрейт.
- **2.3** — надёжное определение пути файла, аккуратный выбор формата,
  Spotify MP3/WAV, сохранение на рабочий стол, поиск трека по длительности.
- **2.2** — скачивание с YouTube и других сайтов (yt-dlp), музыка из Spotify,
  новая подпись авторов.
- **2.0** — Jarvis и Friday, старые имена собираются через `-Legacy`.
- **1.9** — Sierra по умолчанию.
- **1.8** — компактный интерфейс, частота кадров 1–60.
- **1.4** — тёмная тема, скачивание TikTok.
- **1.2** — поддержка картинок.
- **1.0** — первый выпуск.

## Благодарности и лицензии

- FFmpeg — https://ffmpeg.org (LGPL/GPL, сборка gyan.dev).
  Программа использует FFmpeg как отдельный процесс.
- yt-dlp — https://github.com/yt-dlp/yt-dlp (Unlicense).
  Программа использует yt-dlp как отдельный процесс.
- Схема скачивания TikTok (короткие ссылки,
  `__UNIVERSAL_DATA_FOR_REHYDRATION__`, `playAddr`) сделана по мотивам
  проекта cobalt: https://github.com/imputnet/cobalt (AGPL-3.0).

Скачивайте только те материалы, которые разрешены правообладателем
и правилами сайта. Spotify не отдаёт MP3 напрямую: программа использует
открытые метаданные страницы и ищет тот же трек на YouTube.
