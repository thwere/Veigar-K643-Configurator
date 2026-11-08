# Veigar K643 Configurator

**Unofficial configuration software for the Redragon Veigar K643**

Copyright (C) 2026 thwere

[English](#english) · [Русский](#русский)

---

## English

### Project status

The project is an experimental Windows application for the Redragon Veigar K643. The v0.3.0-alpha build expands the RGB Studio into a multi-tab lighting workspace.

### Features in v0.3.0-alpha

- Dark WPF interface, including a Windows dark title bar.
- Automatic HID detection for the tested Veigar K643 interface (VID 258A / PID 0049, MI_01 / Col08).
- Full 126-slot RGB frame editor.
- RGB channel sliders, HEX entry, brightness control, colour preview and quick presets.
- Per-slot colour assignment and full-scene transmission.
- Layer stack with Static, Gradient, Animation, AudioReactive and KeyReactive layers.
- Replace, Add and Max layer blending with per-layer opacity.
- Animation frame capture and cyclic playback.
- Raw Input listener filtered to the K643 USB VID/PID.
- Manual physical-key-to-LED-slot calibration.
- Key-reactive Flash, Fade and HoldGlow effects; simultaneous mapped keys are supported.
- Audio-reactive brightness driven by the Windows default output peak meter.
- JSON profiles containing layers, per-slot colours, animation frames and key mappings.
- One 382-byte HID Feature Report per frame, sent away from the WPF dispatcher.

### Hardware and validation status

The user confirmed that the v0.1.0-alpha application illuminated all keys. The v0.3.0-alpha adds slot editing, key mapping, layer rendering, Raw Input and audio meter integration; these new paths still require physical-device validation.

The 126-cell editor represents protocol LED slots. It is deliberately not presented as a verified physical keyboard diagram. Use the calibration tab to associate a physical key with a slot. Correct neighbour and spatial ripple effects require a complete verified geometry map.

### Not yet included

- Verified physical keyboard geometry and neighbour relationships.
- Ripple and neighbour-ripple effects.
- Frequency-band / spectrum audio visualisation.
- Macro editor and playback.
- Software key remapping.
- Installer and updater.

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

Это экспериментальное Windows-приложение для Redragon Veigar K643. В версии v0.3.0-alpha RGB Studio расширена до многостраничной рабочей среды для управления подсветкой.

### Возможности v0.3.0-alpha

- Тёмный интерфейс WPF и тёмная системная строка заголовка Windows.
- Автоматическое обнаружение проверенного HID-интерфейса K643 (VID 258A / PID 0049, MI_01 / Col08).
- Редактор полного RGB-кадра для 126 LED-слотов.
- Ползунки RGB, ввод HEX, регулировка яркости, предпросмотр и быстрые пресеты.
- Назначение индивидуального цвета LED-слотам и отправка полной сцены.
- Слои Static, Gradient, Animation, AudioReactive и KeyReactive.
- Режимы наложения Replace, Add и Max, прозрачность каждого слоя.
- Захват кадров анимации и циклическое воспроизведение.
- Raw Input с фильтрацией по USB VID/PID клавиатуры K643.
- Ручная калибровка соответствия физических клавиш LED-слотам.
- Эффекты нажатия Flash, Fade и HoldGlow; поддерживается одновременное нажатие нескольких назначенных клавиш.
- Реакция на уровень звука через измеритель пикового уровня стандартного аудиовыхода Windows.
- JSON-профили со слоями, цветами слотов, кадрами анимации и привязками клавиш.
- Один HID Feature Report размером 382 байта на кадр; отправка выполняется вне потока интерфейса.

### Состояние проверки оборудования

Пользователь подтвердил, что приложение v0.1.0-alpha зажгло все клавиши. В v0.3.0-alpha добавлены редактор слотов, привязка клавиш, компоновщик слоёв, Raw Input и аудиометр; эти новые функции ещё требуется проверить на физической клавиатуре.

Сетка из 126 ячеек отображает адреса LED-слотов протокола, а не подтверждённую физическую раскладку клавиатуры. Для связи физической клавиши со слотом используй вкладку калибровки. Для корректных ripple-эффектов и соседних клавиш сначала нужна полная проверенная геометрическая карта.

### Пока не реализовано

- Проверенная физическая геометрия клавиатуры и связи соседних клавиш.
- Ripple и neighbour-ripple.
- Анализ звука по частотным полосам и спектру.
- Редактор макросов и их воспроизведение.
- Программное переназначение клавиш.
- Установщик и механизм обновления.

### Целевые платформы

- Windows 10 x64.
- Windows 11 x64 (требуется тестирование).
- Redragon Veigar K643, подключённая по USB.

### Независимость проекта

Это неофициальный независимый проект сообщества. Он не связан с Redragon, не одобрен и не поддерживается компанией Redragon. Названия Redragon и Veigar принадлежат соответствующим правообладателям.

### Лицензия

Исходный код распространяется под GNU GPL версии 3 или, по выбору пользователя, любой более поздней версии. Полный текст находится в файле [LICENSE](LICENSE).

Гарантии не предоставляются. Программа находится в экспериментальной разработке.
