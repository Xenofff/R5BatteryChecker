# Attack Shark R5 Ultra - Battery Tray Monitor

# Attack Shark R5 Ultra - Battery Tray Monitor

A lightweight standalone Windows utility that displays the battery level of your **Attack Shark R5 Ultra** mouse directly in the system tray (taskbar notification area).

## Features
- **Single `.exe` binary**: Only ~17 KB with zero external runtimes or dependencies.
- **Real-time Tray Icon**: Always visible battery percentage with color coding (Green / Yellow / Red).
- **Charging Indicator**: Shows a lightning icon ⚡ and "Charging" status when plugged in via cable.
- **Resource Friendly**: Low polling frequency with negligible CPU and RAM footprint.
- **Dual Mode Support**: Works both via 2.4GHz wireless dongle and USB cable.
- **Auto-start**: Toggle "Run on Windows Startup" in one click from the tray context menu.

## Quick Start
1. Download `R5BatteryChecker.exe` from the [Releases](../../releases) tab.
2. Run the executable.
3. Check your system tray near the clock for the battery icon.
4. Right-click the icon to manage auto-start or refresh status manually.


Легковесная автономная утилита для Windows, которая отображает уровень заряда аккумулятора мыши **Attack Shark R5 Ultra** в системном трее (рядом с часами).

## Возможности
- **Один файл `.exe`**: Размер всего ~17 КБ, никаких сторонних зависимостей или библиотек.
- **Индикация в реальном времени**: В трее всегда виден точный процент заряда с цветовым кодированием (зеленый / желтый / красный).
- **Индикатор зарядки**: При подключении кабеля отображается иконка молнии ⚡ и статус «Заряжается».
- **Энергоэффективность**: Опрос происходит в фоне каждые 30 секунд без нагрузки на CPU и оперативную память.
- **Поддержка обоих режимов**: Работает как при подключении через беспроводной донгл (4K/8K), так и по проводу.
- **Автозагрузка**: В контекстном меню (правый клик по иконке) можно в один клик включить или выключить «Автозагрузка с Windows».

## Файлы
- `R5BatteryChecker.exe` — готовая утилита (можно скопировать в любую папку и запускать).
- `Program.cs` — исходный код C#.
- `app.manifest` — манифест с поддержкой High-DPI и современных стилей Windows.
- `build.bat` — скрипт быстрой пересборки через встроенный в Windows компилятор `csc.exe`.

## Как пользоваться
1. Запустите `R5BatteryChecker.exe`.
2. В трее (в области уведомлений таскбара) появится иконка с процентом заряда вашей мыши.
3. Наведите курсор на иконку, чтобы увидеть всплывающую подсказку со статусом.
4. Нажмите правой кнопкой мыши по иконке, чтобы открыть меню («Обновить сейчас», «Автозагрузка с Windows», «Выход»).
