#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <mmsystem.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#define PATH_BUFFER_SIZE 8192

static const wchar_t* kProductName = L"\u5927\u72d7\u5927\u72d7\u53eb\u53eb\u53eb";
static const wchar_t* kEnglishName = L"Big Dog, Bark Bark Bark";
static const wchar_t* kAppFolderName = L"BigDogBark";
static const wchar_t* kExeName = L"BigDogBark.exe";
static const wchar_t* kSoundName = L"codex-complete.mp3";
static const wchar_t* kOnShortcutName = L"Codex \u63d0\u793a\u97f3 - \u5f00\u542f.lnk";
static const wchar_t* kOffShortcutName = L"Codex \u63d0\u793a\u97f3 - \u5173\u95ed.lnk";
static BOOL g_quiet = FALSE;

static BOOL file_exists(const wchar_t* path) {
  DWORD attributes = GetFileAttributesW(path);
  return attributes != INVALID_FILE_ATTRIBUTES &&
         (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

static BOOL directory_exists(const wchar_t* path) {
  DWORD attributes = GetFileAttributesW(path);
  return attributes != INVALID_FILE_ATTRIBUTES &&
         (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
}

static BOOL create_directories(const wchar_t* path) {
  if (directory_exists(path)) {
    return TRUE;
  }

  int result = SHCreateDirectoryExW(NULL, path, NULL);
  return result == ERROR_SUCCESS || result == ERROR_ALREADY_EXISTS;
}

static void get_environment_string(const wchar_t* name, wchar_t* value, size_t count) {
  value[0] = L'\0';
  if (count == 0) {
    return;
  }

  DWORD length = GetEnvironmentVariableW(name, value, (DWORD)count);
  if (length == 0 || length >= count) {
    value[0] = L'\0';
  }
}

static void get_module_path(wchar_t* path, size_t count) {
  DWORD length = GetModuleFileNameW(NULL, path, (DWORD)count);
  if (length == 0 || length >= count) {
    path[0] = L'\0';
  }
}

static void get_module_directory(wchar_t* directory, size_t count) {
  get_module_path(directory, count);
  PathRemoveFileSpecW(directory);
}

static void get_local_app_data(wchar_t* path, size_t count) {
  if (FAILED(SHGetFolderPathW(NULL, CSIDL_LOCAL_APPDATA, NULL, SHGFP_TYPE_CURRENT, path))) {
    path[0] = L'\0';
  }
}

static void get_user_profile(wchar_t* path, size_t count) {
  if (FAILED(SHGetFolderPathW(NULL, CSIDL_PROFILE, NULL, SHGFP_TYPE_CURRENT, path))) {
    get_environment_string(L"USERPROFILE", path, count);
  }
}

static void get_desktop_directory(wchar_t* path, size_t count) {
  get_environment_string(L"BIGDOGBARK_DESKTOP_DIR", path, count);
  if (path[0] != L'\0') {
    return;
  }

  if (FAILED(SHGetFolderPathW(NULL, CSIDL_DESKTOP, NULL, SHGFP_TYPE_CURRENT, path))) {
    path[0] = L'\0';
  }
}

static void get_app_directory(wchar_t* path, size_t count) {
  get_environment_string(L"BIGDOGBARK_CONFIG_DIR", path, count);
  if (path[0] != L'\0') {
    return;
  }

  wchar_t localAppData[PATH_BUFFER_SIZE];
  get_local_app_data(localAppData, PATH_BUFFER_SIZE);
  PathCombineW(path, localAppData, kAppFolderName);
}

static void get_codex_home(wchar_t* path, size_t count) {
  get_environment_string(L"BIGDOGBARK_CODEX_HOME", path, count);
  if (path[0] != L'\0') {
    return;
  }

  get_environment_string(L"CODEX_HOME", path, count);
  if (path[0] != L'\0') {
    return;
  }

  wchar_t profile[PATH_BUFFER_SIZE];
  get_user_profile(profile, PATH_BUFFER_SIZE);
  PathCombineW(path, profile, L".codex");
}

static void get_hook_path(wchar_t* path, size_t count) {
  wchar_t codexHome[PATH_BUFFER_SIZE];
  get_codex_home(codexHome, PATH_BUFFER_SIZE);
  PathCombineW(path, codexHome, L"hooks.json");
}

static void get_installed_exe_path(wchar_t* path, size_t count) {
  wchar_t appDirectory[PATH_BUFFER_SIZE];
  get_app_directory(appDirectory, PATH_BUFFER_SIZE);
  PathCombineW(path, appDirectory, kExeName);
}

static void get_installed_sound_path(wchar_t* path, size_t count) {
  wchar_t appDirectory[PATH_BUFFER_SIZE];
  get_app_directory(appDirectory, PATH_BUFFER_SIZE);
  PathCombineW(path, appDirectory, kSoundName);
}

static void get_enabled_marker_path(wchar_t* path, size_t count) {
  wchar_t appDirectory[PATH_BUFFER_SIZE];
  get_app_directory(appDirectory, PATH_BUFFER_SIZE);
  PathCombineW(path, appDirectory, L"enabled");
}

static BOOL is_enabled(void) {
  wchar_t marker[PATH_BUFFER_SIZE];
  get_enabled_marker_path(marker, PATH_BUFFER_SIZE);
  return file_exists(marker);
}

static void set_enabled(BOOL enabled) {
  wchar_t appDirectory[PATH_BUFFER_SIZE];
  wchar_t marker[PATH_BUFFER_SIZE];
  get_app_directory(appDirectory, PATH_BUFFER_SIZE);
  create_directories(appDirectory);
  get_enabled_marker_path(marker, PATH_BUFFER_SIZE);

  if (enabled) {
    HANDLE file = CreateFileW(
        marker,
        GENERIC_WRITE,
        FILE_SHARE_READ,
        NULL,
        CREATE_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        NULL);
    if (file != INVALID_HANDLE_VALUE) {
      CloseHandle(file);
    }
  } else {
    DeleteFileW(marker);
  }
}

static void show_message(const wchar_t* text, UINT flags) {
  if (g_quiet) {
    return;
  }
  MessageBoxW(NULL, text, kProductName, flags);
}

static void write_stdout_text(const char* text) {
  HANDLE output = GetStdHandle(STD_OUTPUT_HANDLE);
  if (output == NULL || output == INVALID_HANDLE_VALUE) {
    return;
  }

  DWORD written = 0;
  WriteFile(output, text, (DWORD)strlen(text), &written, NULL);
}

static void drain_stdin_pipe(void) {
  HANDLE input = GetStdHandle(STD_INPUT_HANDLE);
  if (input == NULL || input == INVALID_HANDLE_VALUE) {
    return;
  }

  if (GetFileType(input) != FILE_TYPE_PIPE) {
    return;
  }

  char buffer[4096];
  while (WaitForSingleObject(input, 50) == WAIT_OBJECT_0) {
    DWORD read = 0;
    if (!ReadFile(input, buffer, sizeof(buffer), &read, NULL) || read == 0) {
      break;
    }
  }
}

static BOOL find_sound_near_executable(wchar_t* soundPath, size_t count) {
  wchar_t moduleDirectory[PATH_BUFFER_SIZE];
  wchar_t parentDirectory[PATH_BUFFER_SIZE];
  wchar_t candidate[PATH_BUFFER_SIZE];

  get_module_directory(moduleDirectory, PATH_BUFFER_SIZE);

  PathCombineW(candidate, moduleDirectory, kSoundName);
  if (file_exists(candidate)) {
    wcsncpy(soundPath, candidate, count - 1);
    soundPath[count - 1] = L'\0';
    return TRUE;
  }

  PathCombineW(candidate, moduleDirectory, L"assets");
  PathCombineW(candidate, candidate, kSoundName);
  if (file_exists(candidate)) {
    wcsncpy(soundPath, candidate, count - 1);
    soundPath[count - 1] = L'\0';
    return TRUE;
  }

  wcsncpy(parentDirectory, moduleDirectory, PATH_BUFFER_SIZE - 1);
  parentDirectory[PATH_BUFFER_SIZE - 1] = L'\0';
  PathRemoveFileSpecW(parentDirectory);
  PathCombineW(candidate, parentDirectory, L"assets");
  PathCombineW(candidate, candidate, kSoundName);
  if (file_exists(candidate)) {
    wcsncpy(soundPath, candidate, count - 1);
    soundPath[count - 1] = L'\0';
    return TRUE;
  }

  wchar_t currentDirectory[PATH_BUFFER_SIZE];
  if (GetCurrentDirectoryW(PATH_BUFFER_SIZE, currentDirectory) > 0) {
    PathCombineW(candidate, currentDirectory, kSoundName);
    if (file_exists(candidate)) {
      wcsncpy(soundPath, candidate, count - 1);
      soundPath[count - 1] = L'\0';
      return TRUE;
    }
  }

  soundPath[0] = L'\0';
  return FALSE;
}

static BOOL write_ascii_file(const wchar_t* path, const char* content) {
  HANDLE file = CreateFileW(
      path,
      GENERIC_WRITE,
      FILE_SHARE_READ,
      NULL,
      CREATE_ALWAYS,
      FILE_ATTRIBUTE_NORMAL,
      NULL);
  if (file == INVALID_HANDLE_VALUE) {
    return FALSE;
  }

  DWORD length = (DWORD)strlen(content);
  DWORD written = 0;
  BOOL ok = length == 0 || WriteFile(file, content, length, &written, NULL);
  CloseHandle(file);
  return ok && written == length;
}

static BOOL run_process_and_wait(wchar_t* commandLine, DWORD timeoutMilliseconds) {
  STARTUPINFOW startupInfo;
  PROCESS_INFORMATION processInfo;
  ZeroMemory(&startupInfo, sizeof(startupInfo));
  ZeroMemory(&processInfo, sizeof(processInfo));
  startupInfo.cb = sizeof(startupInfo);

  if (!CreateProcessW(
          NULL,
          commandLine,
          NULL,
          NULL,
          FALSE,
          CREATE_NO_WINDOW,
          NULL,
          NULL,
          &startupInfo,
          &processInfo)) {
    return FALSE;
  }

  DWORD waitResult = WaitForSingleObject(processInfo.hProcess, timeoutMilliseconds);
  DWORD exitCode = 1;
  if (waitResult == WAIT_OBJECT_0) {
    GetExitCodeProcess(processInfo.hProcess, &exitCode);
  }

  CloseHandle(processInfo.hThread);
  CloseHandle(processInfo.hProcess);
  return waitResult == WAIT_OBJECT_0 && exitCode == 0;
}

static BOOL run_powershell_script(const char* script) {
  wchar_t tempDirectory[PATH_BUFFER_SIZE];
  wchar_t scriptPath[PATH_BUFFER_SIZE];
  wchar_t hookPath[PATH_BUFFER_SIZE];
  wchar_t installedExe[PATH_BUFFER_SIZE];
  wchar_t desktopDirectory[PATH_BUFFER_SIZE];
  wchar_t onShortcut[PATH_BUFFER_SIZE];
  wchar_t offShortcut[PATH_BUFFER_SIZE];

  if (GetTempPathW(PATH_BUFFER_SIZE, tempDirectory) == 0) {
    return FALSE;
  }

  swprintf(
      scriptPath,
      PATH_BUFFER_SIZE,
      L"%sBigDogBark-%lu.ps1",
      tempDirectory,
      (unsigned long)GetCurrentProcessId());

  if (!write_ascii_file(scriptPath, script)) {
    return FALSE;
  }

  get_hook_path(hookPath, PATH_BUFFER_SIZE);
  get_installed_exe_path(installedExe, PATH_BUFFER_SIZE);
  get_desktop_directory(desktopDirectory, PATH_BUFFER_SIZE);
  if (desktopDirectory[0] != L'\0') {
    create_directories(desktopDirectory);
  }
  PathCombineW(onShortcut, desktopDirectory, kOnShortcutName);
  PathCombineW(offShortcut, desktopDirectory, kOffShortcutName);

  SetEnvironmentVariableW(L"BIGDOGBARK_EXE", installedExe);
  SetEnvironmentVariableW(L"BIGDOGBARK_HOOK_PATH", hookPath);
  SetEnvironmentVariableW(L"BIGDOGBARK_DESKTOP_DIR", desktopDirectory);
  SetEnvironmentVariableW(L"BIGDOGBARK_ON_SHORTCUT", onShortcut);
  SetEnvironmentVariableW(L"BIGDOGBARK_OFF_SHORTCUT", offShortcut);

  wchar_t systemDirectory[PATH_BUFFER_SIZE];
  wchar_t powerShellPath[PATH_BUFFER_SIZE];
  if (GetSystemDirectoryW(systemDirectory, PATH_BUFFER_SIZE) == 0) {
    wcscpy(systemDirectory, L"C:\\Windows\\System32");
  }
  PathCombineW(powerShellPath, systemDirectory, L"WindowsPowerShell");
  PathCombineW(powerShellPath, powerShellPath, L"v1.0");
  PathCombineW(powerShellPath, powerShellPath, L"powershell.exe");

  if (!file_exists(powerShellPath)) {
    wcscpy(powerShellPath, L"powershell.exe");
  }

  wchar_t commandLine[PATH_BUFFER_SIZE * 2];
  swprintf(
      commandLine,
      PATH_BUFFER_SIZE * 2,
      L"\"%s\" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"%s\"",
      powerShellPath,
      scriptPath);

  wchar_t* mutableCommandLine = _wcsdup(commandLine);
  if (mutableCommandLine == NULL) {
    DeleteFileW(scriptPath);
    return FALSE;
  }

  BOOL success = run_process_and_wait(mutableCommandLine, 15000);
  free(mutableCommandLine);

  SetEnvironmentVariableW(L"BIGDOGBARK_EXE", NULL);
  SetEnvironmentVariableW(L"BIGDOGBARK_HOOK_PATH", NULL);
  SetEnvironmentVariableW(L"BIGDOGBARK_DESKTOP_DIR", NULL);
  SetEnvironmentVariableW(L"BIGDOGBARK_ON_SHORTCUT", NULL);
  SetEnvironmentVariableW(L"BIGDOGBARK_OFF_SHORTCUT", NULL);
  DeleteFileW(scriptPath);

  return success;
}

static const char* kInstallScript =
    "$ErrorActionPreference = 'Stop'\n"
    "$hookPath = $env:BIGDOGBARK_HOOK_PATH\n"
    "$exe = $env:BIGDOGBARK_EXE\n"
    "$directory = Split-Path -Parent $hookPath\n"
    "New-Item -ItemType Directory -Path $directory -Force | Out-Null\n"
    "if (Test-Path -LiteralPath $hookPath) {\n"
    "  $backup = $hookPath + '.bak.BigDogBark.' + (Get-Date -Format 'yyyyMMddHHmmss')\n"
    "  Copy-Item -LiteralPath $hookPath -Destination $backup -Force\n"
    "  $root = Get-Content -LiteralPath $hookPath -Raw -Encoding UTF8 | ConvertFrom-Json\n"
    "} else {\n"
    "  $root = [pscustomobject]@{}\n"
    "}\n"
    "function Test-Ours($value) {\n"
    "  if ($null -eq $value) { return $false }\n"
    "  if ($value -is [string]) {\n"
    "    return (($value -match 'BigDogBark\\.exe') -and ($value -match '--hook')) -or ($value -match 'notify-if-unfocused\\.ps1')\n"
    "  }\n"
    "  if ($value -is [System.Collections.IEnumerable] -and $value -isnot [string]) {\n"
    "    foreach ($item in $value) { if (Test-Ours $item) { return $true } }\n"
    "    return $false\n"
    "  }\n"
    "  foreach ($property in $value.PSObject.Properties) {\n"
    "    if (Test-Ours $property.Value) { return $true }\n"
    "  }\n"
    "  return $false\n"
    "}\n"
    "if (-not $root.PSObject.Properties['hooks']) {\n"
    "  $root | Add-Member -NotePropertyName hooks -NotePropertyValue ([pscustomobject]@{}) -Force\n"
    "}\n"
    "$stop = @()\n"
    "if ($root.hooks.PSObject.Properties['Stop']) {\n"
    "  foreach ($group in @($root.hooks.Stop)) {\n"
    "    $handlers = @()\n"
    "    if ($group.PSObject.Properties['hooks']) {\n"
    "      foreach ($handler in @($group.hooks)) {\n"
    "        if (-not (Test-Ours $handler)) { $handlers += $handler }\n"
    "      }\n"
    "    }\n"
    "    if ($handlers.Count -gt 0) {\n"
    "      $group.hooks = $handlers\n"
    "      $stop += $group\n"
    "    }\n"
    "  }\n"
    "}\n"
    "$stop += [pscustomobject]@{\n"
    "  hooks = @([pscustomobject]@{\n"
    "    type = 'command'\n"
    "    command = ('cmd.exe /d /s /c \"\"' + $exe + '\" --hook\"')\n"
    "    timeout = 5\n"
    "  })\n"
    "}\n"
    "$root.hooks | Add-Member -NotePropertyName Stop -NotePropertyValue $stop -Force\n"
    "$json = $root | ConvertTo-Json -Depth 30\n"
    "[System.IO.File]::WriteAllText($hookPath, $json, (New-Object System.Text.UTF8Encoding($false)))\n"
    "Remove-Item -LiteralPath $env:BIGDOGBARK_OFF_SHORTCUT -Force -ErrorAction SilentlyContinue\n"
    "$shell = New-Object -ComObject WScript.Shell\n"
    "$shortcut = $shell.CreateShortcut($env:BIGDOGBARK_ON_SHORTCUT)\n"
    "$shortcut.TargetPath = $exe\n"
    "$shortcut.Arguments = '--toggle'\n"
    "$shortcut.WorkingDirectory = Split-Path -Parent $exe\n"
    "$shortcut.Description = 'Toggle Codex completion sound'\n"
    "$shortcut.Hotkey = 'CTRL+ALT+B'\n"
    "$shortcut.IconLocation = $exe + ',0'\n"
    "$shortcut.Save()\n";

static const char* kUninstallScript =
    "$ErrorActionPreference = 'Stop'\n"
    "$hookPath = $env:BIGDOGBARK_HOOK_PATH\n"
    "if (-not (Test-Path -LiteralPath $hookPath)) { exit 0 }\n"
    "$backup = $hookPath + '.bak.BigDogBark.' + (Get-Date -Format 'yyyyMMddHHmmss')\n"
    "Copy-Item -LiteralPath $hookPath -Destination $backup -Force\n"
    "$root = Get-Content -LiteralPath $hookPath -Raw -Encoding UTF8 | ConvertFrom-Json\n"
    "function Test-Ours($value) {\n"
    "  if ($null -eq $value) { return $false }\n"
    "  if ($value -is [string]) {\n"
    "    return (($value -match 'BigDogBark\\.exe') -and ($value -match '--hook')) -or ($value -match 'notify-if-unfocused\\.ps1')\n"
    "  }\n"
    "  if ($value -is [System.Collections.IEnumerable] -and $value -isnot [string]) {\n"
    "    foreach ($item in $value) { if (Test-Ours $item) { return $true } }\n"
    "    return $false\n"
    "  }\n"
    "  foreach ($property in $value.PSObject.Properties) {\n"
    "    if (Test-Ours $property.Value) { return $true }\n"
    "  }\n"
    "  return $false\n"
    "}\n"
    "if (-not $root.PSObject.Properties['hooks']) { exit 0 }\n"
    "if (-not $root.hooks.PSObject.Properties['Stop']) { exit 0 }\n"
    "$stop = @()\n"
    "foreach ($group in @($root.hooks.Stop)) {\n"
    "  $handlers = @()\n"
    "  if ($group.PSObject.Properties['hooks']) {\n"
    "    foreach ($handler in @($group.hooks)) {\n"
    "      if (-not (Test-Ours $handler)) { $handlers += $handler }\n"
    "    }\n"
    "  }\n"
    "  if ($handlers.Count -gt 0) {\n"
    "    $group.hooks = $handlers\n"
    "    $stop += $group\n"
    "  }\n"
    "}\n"
    "$root.hooks | Add-Member -NotePropertyName Stop -NotePropertyValue $stop -Force\n"
    "$json = $root | ConvertTo-Json -Depth 30\n"
    "[System.IO.File]::WriteAllText($hookPath, $json, (New-Object System.Text.UTF8Encoding($false)))\n";

static void get_shortcut_path(BOOL enabled, wchar_t* path, size_t count) {
  wchar_t desktopDirectory[PATH_BUFFER_SIZE];
  get_desktop_directory(desktopDirectory, PATH_BUFFER_SIZE);
  PathCombineW(path, desktopDirectory, enabled ? kOnShortcutName : kOffShortcutName);
}

static void update_shortcut_after_toggle(void) {
  BOOL enabled = is_enabled();
  wchar_t desiredPath[PATH_BUFFER_SIZE];
  wchar_t otherPath[PATH_BUFFER_SIZE];
  get_shortcut_path(enabled, desiredPath, PATH_BUFFER_SIZE);
  get_shortcut_path(!enabled, otherPath, PATH_BUFFER_SIZE);

  if (file_exists(otherPath)) {
    DeleteFileW(desiredPath);
    MoveFileW(otherPath, desiredPath);
  }
}

static BOOL copy_install_files(wchar_t* errorMessage, size_t errorCount) {
  wchar_t modulePath[PATH_BUFFER_SIZE];
  wchar_t appDirectory[PATH_BUFFER_SIZE];
  wchar_t installedExe[PATH_BUFFER_SIZE];
  wchar_t installedSound[PATH_BUFFER_SIZE];
  wchar_t sourceSound[PATH_BUFFER_SIZE];

  get_module_path(modulePath, PATH_BUFFER_SIZE);
  get_app_directory(appDirectory, PATH_BUFFER_SIZE);
  get_installed_exe_path(installedExe, PATH_BUFFER_SIZE);
  get_installed_sound_path(installedSound, PATH_BUFFER_SIZE);

  if (!create_directories(appDirectory)) {
    wcsncpy(errorMessage, L"Unable to create the application directory.", errorCount - 1);
    errorMessage[errorCount - 1] = L'\0';
    return FALSE;
  }

  if (_wcsicmp(modulePath, installedExe) != 0) {
    if (!find_sound_near_executable(sourceSound, PATH_BUFFER_SIZE)) {
      wcsncpy(
          errorMessage,
          L"codex-complete.mp3 was not found.\r\nKeep BigDogBark.exe and codex-complete.mp3 in the same folder.",
          errorCount - 1);
      errorMessage[errorCount - 1] = L'\0';
      return FALSE;
    }

    if (!CopyFileW(modulePath, installedExe, TRUE)) {
      wcsncpy(errorMessage, L"Unable to copy BigDogBark.exe.", errorCount - 1);
      errorMessage[errorCount - 1] = L'\0';
      return FALSE;
    }

    if (!CopyFileW(sourceSound, installedSound, TRUE)) {
      wcsncpy(errorMessage, L"Unable to copy codex-complete.mp3.", errorCount - 1);
      errorMessage[errorCount - 1] = L'\0';
      return FALSE;
    }
  } else if (!file_exists(installedSound)) {
    if (!find_sound_near_executable(sourceSound, PATH_BUFFER_SIZE)) {
      wcsncpy(errorMessage, L"codex-complete.mp3 was not found.", errorCount - 1);
      errorMessage[errorCount - 1] = L'\0';
      return FALSE;
    }

    if (!CopyFileW(sourceSound, installedSound, TRUE)) {
      wcsncpy(errorMessage, L"Unable to copy codex-complete.mp3.", errorCount - 1);
      errorMessage[errorCount - 1] = L'\0';
      return FALSE;
    }
  }

  return TRUE;
}

static int install(BOOL showSuccess) {
  wchar_t errorMessage[1024];
  if (!copy_install_files(errorMessage, 1024)) {
    show_message(errorMessage, MB_OK | MB_ICONERROR);
    return 2;
  }

  wchar_t marker[PATH_BUFFER_SIZE];
  get_enabled_marker_path(marker, PATH_BUFFER_SIZE);
  if (!file_exists(marker)) {
    set_enabled(TRUE);
  }

  if (!run_powershell_script(kInstallScript)) {
    show_message(
        L"Unable to update Codex hooks.json or create the desktop shortcut.",
        MB_OK | MB_ICONERROR);
    return 3;
  }

  if (showSuccess) {
    show_message(
        L"\u5b89\u88c5\u5b8c\u6210\u3002\r\n\r\n"
        L"\u684c\u9762\u5feb\u6377\u65b9\u5f0f\uff1aCodex \u63d0\u793a\u97f3\r\n"
        L"\u5feb\u6377\u952e\uff1aCtrl+Alt+B\r\n\r\n"
        L"\u8bf7\u91cd\u542f Codex\uff0c\u5e76\u5728 /hooks \u4e2d\u4fe1\u4efb\u4e00\u6b21 BigDogBark.exe --hook\u3002",
        MB_OK | MB_ICONINFORMATION);
  }

  return 0;
}

static int uninstall(void) {
  run_powershell_script(kUninstallScript);

  wchar_t onShortcut[PATH_BUFFER_SIZE];
  wchar_t offShortcut[PATH_BUFFER_SIZE];
  get_shortcut_path(TRUE, onShortcut, PATH_BUFFER_SIZE);
  get_shortcut_path(FALSE, offShortcut, PATH_BUFFER_SIZE);
  DeleteFileW(onShortcut);
  DeleteFileW(offShortcut);

  show_message(
      L"Codex Hook and desktop shortcuts were removed.\r\n"
      L"You may now delete %LOCALAPPDATA%\\BigDogBark if it is no longer needed.",
      MB_OK | MB_ICONINFORMATION);
  return 0;
}

static int run_toggle(void) {
  set_enabled(!is_enabled());
  update_shortcut_after_toggle();
  return 0;
}

static int run_hook(void) {
  drain_stdin_pipe();
  write_stdout_text("{}\n");

  if (!is_enabled()) {
    return 0;
  }

  wchar_t executablePath[PATH_BUFFER_SIZE];
  get_module_path(executablePath, PATH_BUFFER_SIZE);
  if (executablePath[0] == L'\0' || !file_exists(executablePath)) {
    return 0;
  }

  wchar_t commandLine[PATH_BUFFER_SIZE * 2];
  swprintf(commandLine, PATH_BUFFER_SIZE * 2, L"\"%s\" --play", executablePath);

  wchar_t* mutableCommandLine = _wcsdup(commandLine);
  if (mutableCommandLine == NULL) {
    return 0;
  }

  STARTUPINFOW startupInfo;
  PROCESS_INFORMATION processInfo;
  ZeroMemory(&startupInfo, sizeof(startupInfo));
  ZeroMemory(&processInfo, sizeof(processInfo));
  startupInfo.cb = sizeof(startupInfo);

  if (CreateProcessW(
          executablePath,
          mutableCommandLine,
          NULL,
          NULL,
          FALSE,
          CREATE_NO_WINDOW,
          NULL,
          NULL,
          &startupInfo,
          &processInfo)) {
    CloseHandle(processInfo.hThread);
    CloseHandle(processInfo.hProcess);
  }

  free(mutableCommandLine);
  return 0;
}

static int run_playback(void) {
  wchar_t soundPath[PATH_BUFFER_SIZE];
  get_installed_sound_path(soundPath, PATH_BUFFER_SIZE);

  if (!file_exists(soundPath)) {
    MessageBeep(MB_ICONASTERISK);
    Sleep(700);
    return 0;
  }

  wchar_t openCommand[PATH_BUFFER_SIZE * 2];
  swprintf(
      openCommand,
      PATH_BUFFER_SIZE * 2,
      L"open \"%s\" type mpegvideo alias bigdogbark",
      soundPath);

  if (mciSendStringW(openCommand, NULL, 0, NULL) == 0) {
    mciSendStringW(L"play bigdogbark wait", NULL, 0, NULL);
    mciSendStringW(L"close bigdogbark", NULL, 0, NULL);
  } else {
    MessageBeep(MB_ICONASTERISK);
    Sleep(700);
  }

  return 0;
}

static BOOL has_argument(int argc, wchar_t** argv, const wchar_t* expected) {
  int index;
  for (index = 1; index < argc; ++index) {
    if (_wcsicmp(argv[index], expected) == 0) {
      return TRUE;
    }
  }
  return FALSE;
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previousInstance, PWSTR commandLine, int showCommand) {
  (void)instance;
  (void)previousInstance;
  (void)commandLine;
  (void)showCommand;

  int argc = 0;
  wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);
  int result = 0;

  if (argv == NULL) {
    return 1;
  }

  g_quiet = has_argument(argc, argv, L"--quiet");

  if (has_argument(argc, argv, L"--hook")) {
    result = run_hook();
  } else if (has_argument(argc, argv, L"--play")) {
    result = run_playback();
  } else if (has_argument(argc, argv, L"--toggle")) {
    result = run_toggle();
  } else if (has_argument(argc, argv, L"--on")) {
    set_enabled(TRUE);
    update_shortcut_after_toggle();
  } else if (has_argument(argc, argv, L"--off")) {
    set_enabled(FALSE);
    update_shortcut_after_toggle();
  } else if (has_argument(argc, argv, L"--uninstall")) {
    result = uninstall();
  } else if (has_argument(argc, argv, L"--install")) {
    result = install(!g_quiet);
  } else {
    result = install(!g_quiet);
  }

  LocalFree(argv);
  return result;
}
