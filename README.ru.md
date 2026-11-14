# Veigar K643 Configurator

Минимальная WPF-оболочка конфигуратора клавиатуры Redragon Veigar K643.

## Текущая базовая версия: v0.0.1

В этой версии намеренно оставлены только окно приложения и подготовленное изображение клавиатуры. Сейчас клавиатура служит визуальной основой: физический ввод, RGB-управление, HID-связь и интерактивность клавиш в эту сборку пока не входят.

Исходный код активной версии:

`src/VeigarK643.Shell/`

Изображение клавиатуры:

`assets/branding/key.png`

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

Репозиторий намеренно возвращён к минимальной оболочке. Старые экспериментальные реализации RGB Studio, HID, Raw Input, профилей и слоёв подсветки удалены из текущего дерева исходников, чтобы их нельзя было спутать с активным проектом.

## Лицензия

См. [LICENSE](LICENSE).
