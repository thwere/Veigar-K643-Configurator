# Veigar K643 Configurator

Минимальная WPF-оболочка конфигуратора клавиатуры Redragon Veigar K643.

## Текущее состояние разработки: `main`

Клавиатура в приложении состоит из трёх слоёв: RGB-подсветка, схема клавиатуры и невидимые хитбоксы мыши. При удержании кнопки мыши над клавишей загорается соответствующая зона. Физическая клавиатура не используется для определения нажатий. Нажатия виртуальных клавиш передаются на клавиатуру через HID Feature Report с использованием измеренной карты из 126 LED-слотов.

Контрольная версия `v0.0.3` скрывает хитбоксы, фиксирует размер окна и добавляет аппаратную RGB-подсветку при нажатии виртуальных клавиш.

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

GitHub Actions проверяет сборку Windows при отправке изменений в `main`. Workflow переносимого prerelease запускается вручную или при отправке тега версии.

## Состояние проекта

Активной остаётся только ветка разработки `VeigarK643.Shell`. Старые экспериментальные реализации RGB Studio, Raw Input, профилей и слоёв подсветки исключены из текущего дерева исходников, чтобы их нельзя было спутать с активным проектом.

## Лицензия

См. [LICENSE](LICENSE).
