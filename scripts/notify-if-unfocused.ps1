#requires -version 5
# Windows port of notify-if-unfocused.sh
#
# Plays a sound when an AI coding agent finishes or needs attention, but only when its
# terminal window is NOT focused. It checks the foreground window, the current console
# window, and its own process tree. For Windows Terminal and other ConPTY hosts, it also
# matches the active window title because the visible terminal process is not normally a
# parent of the shell or hook process.
#
# Run as a hook with:
#   powershell -NoProfile -ExecutionPolicy Bypass -File "<path>\notify-if-unfocused.ps1"

param(
  # Useful for checking the audio path without switching windows.
  [switch]$Force,

  # Prints the focus decision and skips the JSON-only hook output.
  [switch]$Diagnose,

  # Some hook runners, notably Claude Code on Windows, may launch hooks without the
  # terminal console attached. In that case the precise Windows Terminal title probe
  # cannot work, so this treats a foreground terminal host as focused.
  [switch]$TrustForegroundTerminal
)

$ErrorActionPreference = 'SilentlyContinue'

# The desktop toggle controls this marker file. When it is absent, the hook exits
# immediately and Codex keeps using its normal notification behavior.
$soundEnabledMarker = Join-Path $PSScriptRoot 'sound-enabled'
if (-not $Force -and -not $Diagnose -and -not (Test-Path -LiteralPath $soundEnabledMarker -PathType Leaf)) {
  Write-Output '{}'
  exit 0
}

# Win32: GetForegroundWindow + GetWindowThreadProcessId give us the pid that owns the
# currently focused window. There is no pure PowerShell cmdlet for this.
Add-Type -Namespace AiNotify -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr hWnd, out uint pid);
[DllImport("kernel32.dll")] public static extern System.IntPtr GetConsoleWindow();
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(System.IntPtr hWnd, System.Text.StringBuilder text, int count);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(System.IntPtr hWnd, System.Text.StringBuilder text, int count);
'@

function Get-ForegroundWindowInfo {
  $h = [AiNotify.Win]::GetForegroundWindow()
  if ($h -eq [System.IntPtr]::Zero) {
    return [pscustomobject]@{ Hwnd = $h; Pid = 0; ProcessName = ''; Title = ''; ClassName = '' }
  }

  $procId = [uint32]0
  [void][AiNotify.Win]::GetWindowThreadProcessId($h, [ref]$procId)
  $proc = Get-Process -Id ([int]$procId) -ErrorAction SilentlyContinue

  $title = New-Object System.Text.StringBuilder 512
  [void][AiNotify.Win]::GetWindowText($h, $title, $title.Capacity)

  $class = New-Object System.Text.StringBuilder 256
  [void][AiNotify.Win]::GetClassName($h, $class, $class.Capacity)

  return [pscustomobject]@{
    Hwnd = $h
    Pid = [int]$procId
    ProcessName = if ($proc) { $proc.ProcessName } else { '' }
    Title = $title.ToString()
    ClassName = $class.ToString()
  }
}

# Parent pid of a process via one targeted CIM query (cheaper than enumerating all procs).
function Get-ParentPid([int]$procId) {
  $p = Get-CimInstance Win32_Process -Filter "ProcessId=$procId" -ErrorAction SilentlyContinue
  if ($p) { return [int]$p.ParentProcessId }
  return 0
}

function Get-AncestorPids([int]$procId) {
  $ids = New-Object System.Collections.Generic.List[int]
  $cur = $procId
  $guard = 0
  while ($cur -gt 1 -and $guard -lt 64) {
    $ids.Add($cur)
    $next = Get-ParentPid $cur
    if ($next -le 0 -or $next -eq $cur) { break }
    $cur = $next
    $guard++
  }
  return $ids.ToArray()
}

function Test-SameTitle([string]$a, [string]$b) {
  if ([string]::IsNullOrWhiteSpace($a) -or [string]::IsNullOrWhiteSpace($b)) {
    return $false
  }
  return ($a.Trim() -eq $b.Trim())
}

function Test-TerminalHostProcess($fg) {
  $terminalHosts = @(
    'WindowsTerminal', 'wt',
    'wezterm-gui', 'wezterm',
    'alacritty', 'ConEmu64', 'ConEmu', 'mintty'
  )
  return ($fg.ProcessName -in $terminalHosts -or $fg.ClassName -eq 'CASCADIA_HOSTING_WINDOW_CLASS')
}

function Test-ConsoleTitleProbe($fg) {
  if (-not (Test-TerminalHostProcess $fg)) { return $false }

  $oldTitle = ''
  try { $oldTitle = [Console]::Title } catch { return $false }

  $marker = "ai-notify-focus-$PID-$([guid]::NewGuid().ToString('N'))"
  try {
    [Console]::Title = $marker
    Start-Sleep -Milliseconds 150
    $probeFg = Get-ForegroundWindowInfo
    return ($probeFg.Hwnd -eq $fg.Hwnd -and $probeFg.Title -like "*$marker*")
  } catch {
    return $false
  } finally {
    try { [Console]::Title = $oldTitle } catch { }
  }
}

function Test-TerminalIsFocused($fg, [int[]]$ancestors) {
  $script:FocusReason = 'foreground window is not this terminal'
  if ($fg.Pid -le 0) { return $false }

  # Classic console windows are either the current console hwnd or a conhost/OpenConsole
  # window whose parent process is the shell/agent process tree.
  $consoleHwnd = [AiNotify.Win]::GetConsoleWindow()
  if ($consoleHwnd -ne [System.IntPtr]::Zero -and $consoleHwnd -eq $fg.Hwnd) {
    $script:FocusReason = 'foreground hwnd is this console window'
    return $true
  }

  if ($fg.ProcessName -in @('conhost', 'OpenConsole')) {
    $parent = Get-ParentPid $fg.Pid
    if ($parent -gt 0 -and $ancestors -contains $parent) {
      $script:FocusReason = 'foreground console host belongs to this process tree'
      return $true
    }
    return $false
  }

  # Do not treat every foreground ancestor as focused. On Windows, Explorer often starts
  # the terminal, so explorer.exe may be in this process tree while a File Explorer
  # window is focused.
  $directWindowOwners = @(
    'powershell', 'pwsh', 'cmd',
    'bash', 'wsl', 'sh', 'zsh', 'fish', 'nu'
  )
  if ($fg.ProcessName -in $directWindowOwners -and $ancestors -contains $fg.Pid) {
    $script:FocusReason = 'foreground shell process owns the active window'
    return $true
  }

  # Windows Terminal hosts the visible window in WindowsTerminal.exe, while the shell
  # and hook process are attached through ConPTY instead of being descendants of that
  # window process. First try the cheap title comparison; if that is stale or customized,
  # briefly set this console's title and see whether the foreground terminal follows it.
  if (Test-TerminalHostProcess $fg) {
    try {
      if (Test-SameTitle ([Console]::Title) $fg.Title) {
        $script:FocusReason = 'foreground terminal host title matches this console'
        return $true
      }
    } catch { }

    if (Test-ConsoleTitleProbe $fg) {
      $script:FocusReason = 'foreground terminal host responded to this console title probe'
      return $true
    }

    if ($TrustForegroundTerminal) {
      $script:FocusReason = 'foreground terminal host trusted by hook compatibility mode'
      return $true
    }
  }

  return $false
}

# Play the bundled MP3 first, then fall back to a real notification .wav,
# a system sound, and finally a console beep.
function Play-Mp3([string]$Path) {
  try {
    $helper = Join-Path $PSScriptRoot 'Play-CodexSound.ps1'
    if (-not (Test-Path -LiteralPath $helper -PathType Leaf)) {
      return $false
    }

    $powerShellExe = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -LiteralPath $powerShellExe -PathType Leaf)) {
      $powerShellExe = 'powershell.exe'
    }

    $arguments = @(
      '-NoProfile',
      '-NonInteractive',
      '-ExecutionPolicy', 'Bypass',
      '-WindowStyle', 'Hidden',
      '-File', $helper,
      '-Path', $Path
    )
    Start-Process -FilePath $powerShellExe -ArgumentList $arguments -WindowStyle Hidden | Out-Null
    return $true
  } catch {
    return $false
  }
}

function Play-Sound {
  $customMp3Candidates = @(
    (Join-Path $PSScriptRoot 'codex-complete.mp3'),
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\codex-complete.mp3')
  )
  $customMp3 = $customMp3Candidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
  if ($customMp3 -and (Play-Mp3 $customMp3)) {
    return
  }

  $candidates = @(
    "$env:WINDIR\Media\notify.wav",
    "$env:WINDIR\Media\Windows Notify System Generic.wav",
    "$env:WINDIR\Media\Windows Notify.wav",
    "$env:WINDIR\Media\Windows Ding.wav",
    "$env:WINDIR\Media\chimes.wav"
  )
  foreach ($w in $candidates) {
    if (Test-Path -LiteralPath $w) {
      try {
        $player = New-Object System.Media.SoundPlayer $w
        $player.PlaySync()
        return
      } catch { }
    }
  }
  try {
    [System.Media.SystemSounds]::Exclamation.Play()
    Start-Sleep -Milliseconds 700  # SystemSounds.Play is async; give it time before we exit
    return
  } catch { }
  try { [console]::Beep(880, 300) } catch { }
}

$fg = Get-ForegroundWindowInfo
$ancestors = Get-AncestorPids $PID
$focused = Test-TerminalIsFocused $fg $ancestors

if ($Diagnose) {
  Write-Host "Foreground pid       : $($fg.Pid)"
  Write-Host "Foreground process   : $($fg.ProcessName)"
  Write-Host "Foreground class     : $($fg.ClassName)"
  Write-Host "Foreground title     : $($fg.Title)"
  Write-Host "Current console title: $([Console]::Title)"
  Write-Host "Ancestor pids        : $($ancestors -join ', ')"
  Write-Host "Reason              : $FocusReason"
  Write-Host "Decision             : $(if ($Force) { 'FORCE -> play sound' } elseif ($focused) { 'FOCUSED -> stay silent' } else { 'UNFOCUSED -> play sound' })"
}

if ($Force -or -not $focused) { Play-Sound }

# Codex's Stop hook rejects empty/plain-text stdout as invalid; emit a minimal valid JSON
# object. Harmless to Claude Code (no-op decision) and ignored by the opencode plugin.
if (-not $Diagnose) { Write-Output '{}' }
exit 0
