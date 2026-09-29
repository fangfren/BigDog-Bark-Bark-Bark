# 大狗大狗叫叫叫

**English title: Big Dog, Bark Bark Bark**

一个单文件、免安装的 Windows Codex 完成提示音托盘工具。

GitHub: https://github.com/fangfren/BigDog-Bark-Bark-Bark

[中文](#中文说明) | [English](#english)

---

## 中文说明

### 下载与运行

下载仓库根目录中的：

```text
BigDogBark.exe
```

双击运行即可，不需要安装器，也不需要额外脚本。

首次运行会自动完成：

- 将程序复制到 `%LOCALAPPDATA%\BigDogBark`，保证 Hook 路径稳定。
- 将内置提示音释放到本地配置目录。
- 启动右下角系统托盘图标。
- 默认开启自定义提示音。
- 自动写入或修复 Codex `Stop` Hook。

程序目标为 Windows 10/11 自带的 .NET Framework，无需安装额外运行库。

### Codex 首次配置

1. 双击 `BigDogBark.exe`。
2. 右下角出现狗爪托盘图标后，重启 Codex。
3. 在 Codex 中运行 `/hooks`。
4. 找到 `BigDogBark.exe --hook`，审阅并信任一次。

信任完成后，每次 Codex 任务结束都会播放提示音。

### 托盘使用方法

- 左键单击托盘图标：在自定义提示音和默认通知之间切换。
- 右键单击托盘图标：打开完整菜单。

托盘菜单包括：

| 菜单 | 作用 |
| --- | --- |
| 使用自定义提示音 | 开启自定义音频 |
| 使用 Codex 默认通知 | 关闭自定义音频 |
| 试听提示音 | 播放当前提示音 |
| 更换提示音... | 导入 WAV、MP3、M4A、AAC 或 WMA |
| 恢复内置提示音 | 恢复程序内置音频 |
| 安装/修复 Codex Hook | 添加或修复 Codex Hook |
| 移除 Codex Hook | 只删除本项目添加的 Hook |
| 开机自动运行 | 控制是否随 Windows 登录启动 |
| 打开程序目录 | 打开本地配置和音频目录 |
| 退出 | 关闭托盘程序 |

关闭托盘程序不会删除配置。自定义提示音是否生效仍由开启状态和 Hook 决定。

### 更换音频

右键托盘图标，选择“更换提示音...”，然后选择音频文件即可。

支持格式：

```text
WAV / MP3 / M4A / AAC / WMA
```

导入的音频会复制到本地配置目录，因此原音频移动后也不会失效。

内置默认音频为约 `2.84 秒`，最后 `0.2 秒`已经裁掉。

### Hook 说明

程序只会管理自己的 Hook：

```text
BigDogBark.exe --hook
```

安装或更新时会保留 `hooks.json` 中已有的其他 Hook，并自动生成备份。

移除 Hook 时，只会删除本项目的 `BigDogBark.exe --hook`，不会删除其他工具或 Computer Use 的配置。

### 卸载

1. 右键托盘图标，选择“移除 Codex Hook”。
2. 关闭“开机自动运行”。
3. 选择“退出”。
4. 删除目录：

```text
%LOCALAPPDATA%\BigDogBark
```

5. 删除下载的 `BigDogBark.exe`。

### 从源码构建

开发环境使用 Windows PowerShell 和 .NET Framework 自带的 `csc.exe`。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果：

```text
BigDogBark.exe
```

该可执行文件已经内嵌默认音频和托盘图标。

### 文件结构

```text
.
|-- BigDogBark.exe
|-- LICENSE
|-- README.md
|-- build.ps1
|-- assets
|   |-- app-off.ico
|   |-- app-on.ico
|   `-- codex-complete.wav
`-- src
    `-- BigDogBark.cs
```

---

## English

### Download and Run

Download:

```text
BigDogBark.exe
```

Double-click it. No installer or extra script is required.

On first run, the application automatically:

- Copies itself to `%LOCALAPPDATA%\BigDogBark` so the hook path stays stable.
- Extracts the embedded default sound into the local configuration directory.
- Starts a notification-area tray icon.
- Enables the custom sound by default.
- Installs or repairs the Codex `Stop` hook.

The executable targets the .NET Framework included with Windows 10/11. No extra runtime installation is required.

### First Codex Setup

1. Double-click `BigDogBark.exe`.
2. After the paw tray icon appears, restart Codex.
3. Run `/hooks` in Codex.
4. Review and trust the `BigDogBark.exe --hook` entry once.

After that, the custom sound plays whenever a Codex turn finishes.

### Tray Usage

- Left-click the tray icon to switch between the custom sound and normal Codex notifications.
- Right-click the tray icon to open the full menu.

The tray menu includes:

| Menu item | Purpose |
| --- | --- |
| Use custom sound | Enable the custom audio |
| Use Codex default notifications | Disable the custom audio |
| Preview sound | Play the current sound |
| Change sound... | Import WAV, MP3, M4A, AAC, or WMA |
| Restore built-in sound | Restore the embedded audio |
| Install/repair Codex hook | Add or repair the Codex hook |
| Remove Codex hook | Remove only this project's hook |
| Start with Windows | Control automatic startup |
| Open program folder | Open the local configuration and audio directory |
| Exit | Close the tray application |

Exiting the tray application does not delete its configuration. The hook state and enabled flag remain in place.

### Changing the Audio

Right-click the tray icon, choose `Change sound...`, and select an audio file.

Supported formats:

```text
WAV / MP3 / M4A / AAC / WMA
```

The selected file is copied into the local configuration directory, so moving the original file later will not break playback.

The embedded default clip is about `2.84 seconds`; its final `0.2 seconds` has been removed.

### Hook Behavior

The application manages only its own hook:

```text
BigDogBark.exe --hook
```

When installing or updating, existing hooks in `hooks.json` are preserved and backed up.

When removing, only the `BigDogBark.exe --hook` entry is deleted. Other tools and Computer Use configuration remain untouched.

### Uninstall

1. Right-click the tray icon and choose `Remove Codex hook`.
2. Disable `Start with Windows`.
3. Choose `Exit`.
4. Delete:

```text
%LOCALAPPDATA%\BigDogBark
```

5. Delete the downloaded `BigDogBark.exe`.

### Build from Source

The build uses Windows PowerShell and the `csc.exe` included with .NET Framework.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Output:

```text
BigDogBark.exe
```

The executable embeds both the default sound and the tray icons.

### File Structure

```text
.
|-- BigDogBark.exe
|-- LICENSE
|-- README.md
|-- build.ps1
|-- assets
|   |-- app-off.ico
|   |-- app-on.ico
|   `-- codex-complete.wav
`-- src
    `-- BigDogBark.cs
```

### License

MIT License.
