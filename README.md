# 大狗大狗叫叫叫

**English title: Big Dog, Bark Bark Bark**

一个极轻量的 Windows Codex 完成提示音开关。原生 C 程序、无托盘、无常驻进程，使用外置 MP3。

当前版本：**v2.0.0**

[中文](#中文说明) | [English](#english)

---

## 中文说明

### v2.0.0 发布说明

- 从 PowerShell 方案重写为原生 C 程序。
- 主程序约 `25 KB`，外置 MP3 约 `71 KB`。
- 移除托盘和后台常驻进程，空闲资源为 `0`。
- 桌面快捷方式支持 `Ctrl+Alt+B` 快速开关。
- 关闭时保留 Codex 默认通知行为。
- 自动迁移并移除旧的 `notify-if-unfocused.ps1` Hook，避免重复播放。
- 保留 `hooks.json` 中的其他 Hook 和 Computer Use 配置。
- 内置音频内容约 `2.8 秒`。

### 下载与使用

下载：

```text
release\BigDogBark-portable.zip
```

解压后目录中有两个文件：

```text
BigDogBark.exe
codex-complete.mp3
```

双击 `BigDogBark.exe` 即可，无需安装器。

首次运行会自动：

- 将 exe 和 MP3 复制到 `%LOCALAPPDATA%\BigDogBark`。
- 写入或修复 Codex `Stop` Hook。
- 创建桌面快捷方式 `Codex 提示音`。
- 设置快捷键 `Ctrl+Alt+B`。
- 默认开启自定义提示音。

重启 Codex 后，在 `/hooks` 中信任一次 `BigDogBark.exe --hook`，配置完成。

快速开关使用桌面快捷方式：

- 双击 `Codex 提示音` 快捷方式。
- 或按 `Ctrl+Alt+B`。

快捷方式名称会随状态变化：

```text
Codex 提示音 - 开启
Codex 提示音 - 关闭
```

### 更换音频

替换本地文件：

```text
%LOCALAPPDATA%\BigDogBark\codex-complete.mp3
```

保持文件名不变即可。下一次 Codex 完成任务时会使用新的 MP3。

### Hook 行为

程序只添加自己的 Hook：

```text
BigDogBark.exe --hook
```

安装和卸载时会：

- 备份 `hooks.json`。
- 只删除 `BigDogBark.exe --hook`。
- 保留其他 Hook 和 Computer Use 配置。

如果不再需要自定义提示音，可以运行卸载命令，恢复为 Codex 系统默认通知。

### 资源占用

- 原生 exe：约 `25 KB`
- 外置 MP3：约 `71 KB`
- 空闲常驻内存：`0`
- Hook 进程：约 `0.1-0.3 秒`退出
- 音频播放子进程：约 3 秒后退出

### 卸载

运行：

```powershell
& "$env:LOCALAPPDATA\BigDogBark\BigDogBark.exe" --uninstall
```

然后删除：

```text
%LOCALAPPDATA%\BigDogBark
```

如果需要，也可以删除桌面上的 `Codex 提示音` 快捷方式。

### 从源码构建

需要 MSYS2 `clang64`：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建脚本会生成：

```text
release\BigDogBark-portable.zip
```

### 文件结构

```text
.
|-- LICENSE
|-- README.md
|-- app.rc
|-- build.ps1
|-- assets
|   |-- app-on.ico
|   `-- codex-complete.mp3
|-- release
|   `-- BigDogBark-portable.zip
`-- src
    `-- BigDogBark.c
```

---

## English

### v2.0.0 Release Notes

- Rewritten from PowerShell to a native C program.
- The main executable is about `25 KB`; the external MP3 is about `71 KB`.
- The tray and persistent background process were removed. Idle resource usage is `0`.
- The desktop shortcut supports `Ctrl+Alt+B` for fast toggling.
- When disabled, normal Codex notification behavior is preserved.
- The installer removes the legacy `notify-if-unfocused.ps1` hook to prevent duplicate playback.
- Other hooks and Computer Use configuration are preserved.
- The bundled audio is about `2.8 seconds` long.

### Download and Run

Download:

```text
release\BigDogBark-portable.zip
```

After extraction, the folder contains:

```text
BigDogBark.exe
codex-complete.mp3
```

Double-click `BigDogBark.exe`. No installer is required.

On first run, the program automatically:

- Copies the executable and MP3 to `%LOCALAPPDATA%\BigDogBark`.
- Installs or repairs the Codex `Stop` hook.
- Creates the `Codex 提示音` desktop shortcut.
- Assigns the `Ctrl+Alt+B` shortcut key.
- Enables the custom sound by default.

Restart Codex and trust `BigDogBark.exe --hook` once in `/hooks`.

Use the desktop shortcut or press `Ctrl+Alt+B` to toggle the sound.

The shortcut name changes with the current state:

```text
Codex 提示音 - 开启
Codex 提示音 - 关闭
```

### Change the Audio

Replace:

```text
%LOCALAPPDATA%\BigDogBark\codex-complete.mp3
```

Keep the filename unchanged. The next completed Codex turn will use the new MP3.

The bundled clip has its final `0.2 seconds` removed and is about `2.8 seconds` long.

### Hook Behavior

The application manages only its own hook:

```text
BigDogBark.exe --hook
```

Installation and removal automatically:

- Back up `hooks.json`.
- Remove only `BigDogBark.exe --hook`.
- Preserve other hooks and Computer Use configuration.

When the custom sound is no longer needed, run the uninstall command to return to the default Codex notification behavior.

### Resource Usage

- Native executable: about `25 KB`
- External MP3: about `71 KB`
- Persistent memory: `0`
- Hook process: exits in about `0.1-0.3 seconds`
- Playback child process: exits after about 3 seconds

### Uninstall

Run:

```powershell
& "$env:LOCALAPPDATA\BigDogBark\BigDogBark.exe" --uninstall
```

Then delete:

```text
%LOCALAPPDATA%\BigDogBark
```

You may also delete the `Codex 提示音` desktop shortcut.

### Build from Source

MSYS2 `clang64` is required:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The build script generates:

```text
release\BigDogBark-portable.zip
```

### File Structure

```text
.
|-- LICENSE
|-- README.md
|-- app.rc
|-- build.ps1
|-- assets
|   |-- app-on.ico
|   `-- codex-complete.mp3
|-- release
|   `-- BigDogBark-portable.zip
`-- src
    `-- BigDogBark.c
```

### License

MIT License.
