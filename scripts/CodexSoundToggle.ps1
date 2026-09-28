param(
  [ValidateSet('toggle', 'on', 'off', 'status')]
  [string]$Action = 'toggle',
  [switch]$NoGui
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$stateMarker = Join-Path $scriptDir 'sound-enabled'
$desktopDir = [Environment]::GetFolderPath('Desktop')
$shortcutOn = Join-Path $desktopDir 'Codex 提示音 - 开启.lnk'
$shortcutOff = Join-Path $desktopDir 'Codex 提示音 - 关闭.lnk'

function Test-CustomSoundEnabled {
  return Test-Path -LiteralPath $stateMarker -PathType Leaf
}

function Set-CustomSoundEnabled([bool]$Enabled) {
  if ($Enabled) {
    New-Item -ItemType File -Path $stateMarker -Force | Out-Null
  } elseif (Test-Path -LiteralPath $stateMarker) {
    Remove-Item -LiteralPath $stateMarker -Force
  }
}

function Update-ShortcutName {
  $desiredPath = if (Test-CustomSoundEnabled) { $shortcutOn } else { $shortcutOff }
  $existingPath = @($shortcutOn, $shortcutOff) |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

  if ($existingPath -and
      -not [string]::Equals($existingPath, $desiredPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Move-Item -LiteralPath $existingPath -Destination $desiredPath -Force
  }
}

function Set-ToggleState([bool]$Enabled) {
  Set-CustomSoundEnabled $Enabled
  Update-ShortcutName
  return (Test-CustomSoundEnabled)
}

function Invoke-ToggleAction {
  switch ($Action) {
    'on' { return Set-ToggleState $true }
    'off' { return Set-ToggleState $false }
    'status' { return (Test-CustomSoundEnabled) }
    default { return Set-ToggleState (-not (Test-CustomSoundEnabled)) }
  }
}

if ($NoGui) {
  $enabled = [bool](Invoke-ToggleAction)
  if ($enabled) { Write-Output 'ENABLED' } else { Write-Output 'DISABLED' }
  exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Codex 提示音开关'
$form.ClientSize = New-Object System.Drawing.Size(430, 235)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.BackColor = [System.Drawing.Color]::White
$form.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 10)

$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = 'Codex 完成提示音'
$titleLabel.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 16, [System.Drawing.FontStyle]::Bold)
$titleLabel.ForeColor = [System.Drawing.Color]::FromArgb(32, 32, 32)
$titleLabel.Location = New-Object System.Drawing.Point(24, 18)
$titleLabel.Size = New-Object System.Drawing.Size(382, 38)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object System.Drawing.Point(26, 58)
$statusLabel.Size = New-Object System.Drawing.Size(378, 25)

$toggleButton = New-Object System.Windows.Forms.Button
$toggleButton.Location = New-Object System.Drawing.Point(25, 92)
$toggleButton.Size = New-Object System.Drawing.Size(380, 82)
$toggleButton.FlatStyle = 'Flat'
$toggleButton.FlatAppearance.BorderSize = 0
$toggleButton.ForeColor = [System.Drawing.Color]::White
$toggleButton.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 11, [System.Drawing.FontStyle]::Bold)
$toggleButton.Cursor = [System.Windows.Forms.Cursors]::Hand
$toggleButton.UseVisualStyleBackColor = $false

$hintLabel = New-Object System.Windows.Forms.Label
$hintLabel.Text = '点击按钮切换；关闭窗口不会改变当前状态。'
$hintLabel.ForeColor = [System.Drawing.Color]::FromArgb(110, 110, 110)
$hintLabel.Location = New-Object System.Drawing.Point(26, 188)
$hintLabel.Size = New-Object System.Drawing.Size(378, 24)

function Update-ToggleUi {
  if (Test-CustomSoundEnabled) {
    $statusLabel.Text = '当前状态：使用自定义 3 秒提示音'
    $statusLabel.ForeColor = [System.Drawing.Color]::FromArgb(38, 125, 50)
    $toggleButton.Text = "已开启：自定义提示音`r`n点击切换为 Codex 默认通知"
    $toggleButton.BackColor = [System.Drawing.Color]::FromArgb(38, 125, 50)
  } else {
    $statusLabel.Text = '当前状态：使用 Codex 默认通知'
    $statusLabel.ForeColor = [System.Drawing.Color]::FromArgb(100, 100, 100)
    $toggleButton.Text = "已关闭：Codex 默认通知`r`n点击开启自定义提示音"
    $toggleButton.BackColor = [System.Drawing.Color]::FromArgb(92, 92, 92)
  }
}

$toggleButton.Add_Click({
  [void](Invoke-ToggleAction)
  Update-ToggleUi
})

$form.Controls.AddRange(@($titleLabel, $statusLabel, $toggleButton, $hintLabel))
Update-ToggleUi
[void]$form.ShowDialog()
