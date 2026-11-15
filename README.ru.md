# Veigar K643 Configurator

WPF-редактор подсветки клавиатуры Redragon Veigar K643 для Windows.

## Текущие возможности

- Инструменты кисти, заливки, пипетки, выделения и ластика для виртуальной клавиатуры.
- Палитра цвета с настройкой оттенка и насыщенности, ввод HEX и последние использованные цвета.
- Именованные группы клавиш с редактированием состава и Drag & Drop-приоритетом. При пересечении состава приоритет имеет группа, расположенная выше.
- Независимые локальные профили с названием, цветом, порядком запуска, импортом и экспортом JSON.
- История отмены и повтора операций редактора.
- Передача цветов на совместимый HID Feature Report интерфейс Veigar K643, включая обработку переподключения.
- Отдельная утилита редактора раскладки для настройки геометрии хитбоксов клавиш.

Схема клавиатуры встроена из файла `assets/layout/veigar-k643-layout.json`. Координаты заданы в системе изображения размером 599×222. Таблица соответствия клавиш LED-слотам находится в `src/VeigarK643.Shell/VeigarK643HidController.cs`.

## Версия

Текущая линия разработки — **v0.4.0 (pre-release)**. Версии ниже 1.0.0 предназначены для тестирования и постепенного развития функциональности.

## Структура исходников

Окно редактора разделено на отдельные partial-файлы по назначению: обработка клавиш и HID, цветовой редактор, группы, хранение и интерфейс профилей, оформление окна. Общие модели и снимки истории находятся в `EditorModels.cs`.

Это реорганизация исходного кода без изменения интерфейса, формата JSON-профилей и протокола HID. Версия текущей линии разработки — `v0.4.0` (pre-release).

## Сборка

Требования: Windows и .NET 10 SDK с поддержкой WPF.

```powershell
dotnet restore src/VeigarK643.Shell/VeigarK643.Shell.csproj
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --no-restore
```

## Portable-сборка Windows x64

```powershell
dotnet publish src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output publish/win-x64
```

GitHub Actions собирает проекты при отправке изменений и pull request в `main`. При каждом обновлении `main` portable pre-release собирается заново с версией из файла проекта активного приложения.

## Утилита раскладки

`tools/VeigarK643.LayoutEditor/` — отдельная WPF-утилита для редактирования областей клавиш и экспорта JSON-конфигурации.

## Лицензия

См. [LICENSE](LICENSE).
