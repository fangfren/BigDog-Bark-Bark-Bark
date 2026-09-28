# 大狗大狗叫叫叫

**English title: Big Dog, Bark Bark Bark**

Windows Codex completion-sound hook with a desktop toggle.

[中文说明](#中文说明) | [English](#english)

---

## 中文说明

### 工作原理

项目通过 Codex 的 `Stop` Hook 在每轮任务结束时执行一个 PowerShell 脚本。

- 开关开启：播放 `assets\codex-complete.mp3`。
- 开关关闭：Hook 立即退出，保留 Codex 默认通知行为。
- 音频播放放在独立隐藏进程中，Hook 会马上结束，不会因为音频较长而超时。
- 项目不修改 Codex 的 `notify` 配置，因此不会影响 Computer Use 等已有回调。

开关状态由 `scripts\sound-enabled` 标记文件控制：

- 文件存在：自定义提示音开启。
- 文件不存在：自定义提示音关闭。

### 环境要求

- Windows 10 或 Windows 11。
- Windows PowerShell 5.1 或更高版本。
- 已安装 Codex。

### 安装

1. 将项目放在一个长期保留的目录。
2. 在项目根目录打开 PowerShell，运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 codex
```

3. 重启 Codex。
4. 在 Codex 中运行 `/hooks`，审阅并信任 `Stop` Hook。
5. 创建桌面快捷开关。推荐使用项目自带的一键脚本：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Create-DesktopShortcut.ps1
```

运行成功后，终端会显示：

```text
Desktop shortcut created successfully.
```

这时桌面上会出现“Codex 提示音 - 开启”或“Codex 提示音 - 关闭”快捷方式。

如果你想手工创建，按下面的步骤操作：

1. 打开项目文件夹，进入 `scripts` 目录。
2. 在文件资源管理器顶部地址栏中复制完整路径。
3. 在桌面空白处右键，选择“新建” -> “快捷方式”。
4. 在“请键入对象的位置”中粘贴下面这一行，并把 `<项目完整路径>` 替换成你刚刚复制的路径：

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "<项目完整路径>\scripts\CodexSoundToggle.ps1"
```

5. 点击“下一步”。
6. 快捷方式名称填写：

```text
Codex 提示音 - 开启
```

7. 点击“完成”。
8. 如果桌面上没有马上显示，按 `F5` 刷新桌面。

手工创建时，名称必须使用 `Codex 提示音 - 开启`，这样开关脚本才能在开启和关闭状态之间自动改名。

安装后不要随意移动项目目录，因为 Hook 和快捷方式会引用该目录。

### 使用方法

双击桌面上的“Codex 提示音”快捷方式，会打开一个小窗口：

- 开启：使用自定义 MP3。
- 关闭：跳过自定义 MP3，使用 Codex 默认通知。

快捷方式名称会随状态变化：

- `Codex 提示音 - 开启.lnk`
- `Codex 提示音 - 关闭.lnk`

关闭窗口不会改变状态，只有点击窗口中的按钮才会切换。

### 更换音频

直接替换：

```text
assets\codex-complete.mp3
```

文件名保持不变即可。替换音频不需要重新安装或重新信任 Hook，下一次任务结束时会自动使用新音频。

当前 Hook 超时默认是 5 秒。建议使用不超过 4 秒的短音频；如果音频更长，请修改：

```text
%USERPROFILE%\.codex\hooks.json
```

把：

```json
"timeout": 5
```

调整为：

```text
音频时长向上取整 + 2 秒
```

### 关闭与卸载

只关闭自定义声音：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\CodexSoundToggle.ps1 -NoGui -Action off
```

重新开启：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\CodexSoundToggle.ps1 -NoGui -Action on
```

完整卸载：

1. 关闭自定义声音。
2. 从 `%USERPROFILE%\.codex\hooks.json` 删除本项目的 `Stop` Hook。
3. 删除桌面快捷方式。
4. 删除项目目录。

### 文件结构

```text
.
|-- README.md
|-- install.ps1
|-- assets
|   `-- codex-complete.mp3
|-- scripts
|   |-- CodexSoundToggle.ps1
|   |-- Create-DesktopShortcut.ps1
|   |-- notify-if-unfocused.ps1
|   `-- Play-CodexSound.ps1
`-- templates
    `-- codex
        `-- hooks.windows.json
```

### 来源

基础方案来自 [Helias/ai-notify](https://github.com/Helias/ai-notify)，按 MIT License 使用和修改。

---

## English

### How It Works

This project uses a Codex `Stop` hook to run a PowerShell script whenever a turn completes.

- Enabled: play `assets\codex-complete.mp3`.
- Disabled: exit immediately and keep the normal Codex notification behavior.
- Audio playback runs in a separate hidden process, so the hook returns immediately and does not time out.
- The project does not modify Codex's `notify` setting, so existing callbacks such as Computer Use remain intact.

The toggle is controlled by `scripts\sound-enabled`:

- The file exists: the custom sound is enabled.
- The file does not exist: the custom sound is disabled.

### Requirements

- Windows 10 or Windows 11.
- Windows PowerShell 5.1 or newer.
- Codex installed.

### Installation

1. Keep this project in a permanent directory.
2. Open PowerShell in the project root and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 codex
```

3. Restart Codex.
4. Run `/hooks` in Codex, review the `Stop` hook, and trust it.
5. Create the desktop toggle shortcut. The recommended method is the built-in helper:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Create-DesktopShortcut.ps1
```

When it succeeds, the terminal displays:

```text
Desktop shortcut created successfully.
```

The desktop will then contain either `Codex 提示音 - 开启` or `Codex 提示音 - 关闭`.

To create the shortcut manually:

1. Open the project folder and go into the `scripts` directory.
2. Copy the full path from the File Explorer address bar.
3. Right-click an empty area of the desktop and select `New` -> `Shortcut`.
4. In the location field, paste the following line and replace `<PROJECT_ROOT>` with the full path you copied:

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "<PROJECT_ROOT>\scripts\CodexSoundToggle.ps1"
```

5. Click `Next`.
6. Use this shortcut name:

```text
Codex 提示音 - 开启
```

7. Click `Finish`.
8. If the shortcut does not appear immediately, press `F5` on the desktop.

The name must be `Codex 提示音 - 开启` so the toggle can rename it automatically when switching between on and off.

Do not move the project directory after installation because the hook and shortcut reference that location.

### Usage

Double-click the desktop shortcut to open the toggle window:

- On: use the custom MP3.
- Off: skip the custom MP3 and use normal Codex notifications.

The shortcut name changes with the current state:

- `Codex 提示音 - 开启.lnk`
- `Codex 提示音 - 关闭.lnk`

Closing the window does not change the state. Click the button to toggle it.

### Changing the Audio

Replace:

```text
assets\codex-complete.mp3
```

Keep the filename unchanged. Replacing the file does not require reinstalling or trusting the hook again.

The default hook timeout is 5 seconds. Prefer a clip no longer than about 4 seconds. For a longer clip, edit:

```text
%USERPROFILE%\.codex\hooks.json
```

Change:

```json
"timeout": 5
```

to:

```text
rounded-up audio duration + 2 seconds
```

### Disable or Uninstall

Disable only the custom sound:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\CodexSoundToggle.ps1 -NoGui -Action off
```

Enable it again:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\CodexSoundToggle.ps1 -NoGui -Action on
```

Full uninstall:

1. Disable the custom sound.
2. Remove this project's `Stop` hook from `%USERPROFILE%\.codex\hooks.json`.
3. Delete the desktop shortcut.
4. Delete the project directory.

### File Structure

```text
.
|-- README.md
|-- install.ps1
|-- assets
|   `-- codex-complete.mp3
|-- scripts
|   |-- CodexSoundToggle.ps1
|   |-- Create-DesktopShortcut.ps1
|   |-- notify-if-unfocused.ps1
|   `-- Play-CodexSound.ps1
`-- templates
    `-- codex
        `-- hooks.windows.json
```

### Credits

The base approach comes from [Helias/ai-notify](https://github.com/Helias/ai-notify), used and modified under the MIT License.
