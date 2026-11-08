# Veigar K643 Configurator

**Unofficial configuration software for the Redragon Veigar K643**

Copyright (C) 2026 thwere

[English](#english) · [Русский](#русский)

---

## English

### Project status

The project is an experimental Windows application for the Redragon Veigar K643. The first expanded alpha build includes a WPF RGB Studio with custom colour controls and local lighting profiles.

### Current application features

- Automatic HID detection for the tested Veigar K643 interface (VID 258A / PID 0049, MI_01 / Col08).
- Solid RGB lighting across all 126 LED slots.
- RGB channel sliders and six-digit HEX colour input.
- Global brightness adjustment and live output preview.
- Quick colour presets and lights-off command.
- Local JSON profile save/load.
- Single-report HID updates dispatched away from the UI thread.

### Hardware and validation status

The first WPF build successfully lit all keyboard keys during user testing. The expanded editor, profile storage and one-report low-latency transport are included in the v0.2.0-alpha pre-release and require additional hardware validation.

The current UI controls global solid-colour lighting. Experimental slot-to-key mapping and Raw Input key detection are not yet integrated into the application.

### Planned areas

- Verified per-key RGB editing and a visual keyboard layout.
- Lighting layers, animations and reactive effects.
- Frame-based animation editor.
- Audio-reactive and keypress-reactive lighting.
- Further configuration features as protocol research progresses.

### Target platform

- Windows 10 x64.
- Windows 11 x64 (testing required).
- Redragon Veigar K643 connected through USB.

### Independence notice

This is an unofficial community-developed project. It is not affiliated with, endorsed by, or supported by Redragon. Redragon and Veigar are names and/or marks of their respective owners.

### License

The project source code is licensed under the GNU General Public License version 3 or (at your option) any later version. See [LICENSE](LICENSE).

No warranty is provided. This software is experimental.

---

## Русский

### Статус проекта

Проект представляет собой экспериментальное Windows-приложение для Redragon Veigar K643. Первая расширенная alpha-сборка включает RGB Studio с настройкой произвольного цвета и локальными профилями подсветки.

### Возможности текущей сборки

- Автоматическое обнаружение проверенного HID-интерфейса Veigar K643 (VID 258A / PID 0049, MI_01 / Col08).
- Управление общей RGB-подсветкой всех 126 LED-слотов.
- Ползунки каналов R, G и B, ввод цвета в HEX.
- Регулировка общей яркости и предпросмотр итогового цвета.
- Быстрые цветовые пресеты и команда выключения подсветки.
- Сохранение и загрузка локальных JSON-профилей.
- Отправка одного HID-отчёта в фоновом потоке без искусственной трёхсекундной серии повторов.

### Статус проверки оборудования

Первая WPF-сборка успешно зажгла все клавиши при пользовательском тестировании. Расширенный редактор, профили и однократная отправка отчёта входят в v0.2.0-alpha и требуют дополнительной проверки на клавиатуре.

Текущий интерфейс управляет общей одноцветной подсветкой. Экспериментальная карта соответствия LED-слотов клавишам и обнаружение нажатий Raw Input пока не интегрированы в приложение.

### Дальнейшие направления

- Проверенный редактор индивидуальной подсветки клавиш и визуальная модель клавиатуры.
- Слои подсветки, анимации и реактивные эффекты.
- Покадровый редактор анимаций.
- Реакция на звук и нажатия клавиш.
- Дополнительные функции по мере исследования протокола.

### Целевые платформы

- Windows 10 x64.
- Windows 11 x64 (требуется тестирование).
- Redragon Veigar K643, подключённая по USB.

### Независимость проекта

Это неофициальный независимый проект сообщества. Он не связан с Redragon, не одобрен и не поддерживается компанией Redragon. Названия Redragon и Veigar принадлежат соответствующим правообладателям.

### Лицензия

Исходный код распространяется под GNU GPL версии 3 или, по выбору пользователя, любой более поздней версии. Полный текст находится в файле [LICENSE](LICENSE).

Гарантии не предоставляются. Программа находится в экспериментальной разработке.
