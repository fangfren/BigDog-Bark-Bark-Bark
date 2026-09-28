#requires -version 5

$ErrorActionPreference = 'Stop'

$scriptsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptsDir
$toggleScript = Join-Path $scriptsDir 'CodexSoundToggle.ps1'

if (-not (Test-Path -LiteralPath $toggleScript -PathType Leaf)) {
  throw "Toggle script not found: $toggleScript"
}

$desktop = [Environment]::GetFolderPath('Desktop')
$enabled = Test-Path -LiteralPath (Join-Path $scriptsDir 'sound-enabled') -PathType Leaf
$shortcutName = if ($enabled) {
  'Codex 提示音 - 开启.lnk'
} else {
  'Codex 提示音 - 关闭.lnk'
}
$otherShortcutName = if ($enabled) {
  'Codex 提示音 - 关闭.lnk'
} else {
  'Codex 提示音 - 开启.lnk'
}

$shortcutPath = Join-Path $desktop $shortcutName
$otherShortcutPath = Join-Path $desktop $otherShortcutName
Remove-Item -LiteralPath $otherShortcutPath -Force -ErrorAction SilentlyContinue

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = (Get-Command powershell.exe).Source
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$toggleScript`""
$shortcut.WorkingDirectory = $projectRoot
$shortcut.Description = '切换 Codex 自定义完成提示音与默认通知'

$soundIcon = Join-Path $env:SystemRoot 'System32\SndVol.exe'
if (Test-Path -LiteralPath $soundIcon -PathType Leaf) {
  $shortcut.IconLocation = "$soundIcon,0"
}

$shortcut.Save()

Write-Host ''
Write-Host 'Desktop shortcut created successfully.' -ForegroundColor Green
Write-Host "Shortcut: $shortcutPath"
Write-Host 'Double-click it to open the Codex sound toggle.'
