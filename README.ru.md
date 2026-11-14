# Veigar K643 Configurator

Минимальная WPF-оболочка конфигуратора клавиатуры Redragon Veigar K643.

## Текущее состояние разработки: `main`

Клавиатура в приложении состоит из трёх слоёв: RGB-подсветка, схема клавиатуры и увеличенные по центру хитбоксы мыши. При удержании кнопки мыши над клавишей загорается соответствующая зона. Физическая клавиатура больше не используется для подсветки. Связь с устройством через HID и прямое управление его RGB пока не реализованы.

Пререлиз `v0.0.2` добавляет нажатия клавиш мышью в интерфейсе и трёхслойное отображение клавиатуры.

Исходный код активной версии:

`src/VeigarK643.Shell/`

Изображение клавиатуры:

`assets/branding/key.png`

Координаты областей подсветки загружаются из `assets/layout/veigar-k643-layout.json` в системе координат 599×222.

## Сборка

Требования: Windows и .NET 10 SDK с поддержкой WPF.

```powershell
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release
```

## Публикация переносимой версии Windows x64

```powershell
dotnet publish src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output publish/win-x64
```

GitHub Actions собирает текущую базовую версию из `main` и публикует её как prerelease `v0.0.1`.

## Состояние проекта

Активной остаётся только ветка разработки `VeigarK643.Shell`. Старые экспериментальные реализации RGB Studio, HID, Raw Input, профилей и слоёв подсветки исключены из текущего дерева исходников, чтобы их нельзя было спутать с активным проектом.

## Лицензия

См. [LICENSE](LICENSE).
