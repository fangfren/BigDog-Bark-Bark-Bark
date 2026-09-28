param(
  [Parameter(Mandatory = $true)]
  [string]$Path
)

$ErrorActionPreference = 'SilentlyContinue'
$player = $null

try {
  Add-Type -AssemblyName PresentationCore
  $player = New-Object System.Windows.Media.MediaPlayer
  $player.Volume = 1.0
  $player.Open([Uri]$Path)

  $deadline = (Get-Date).AddSeconds(3)
  while (-not $player.NaturalDuration.HasTimeSpan -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 50
  }

  $player.Play()
  $waitMs = 3000
  if ($player.NaturalDuration.HasTimeSpan) {
    $waitMs = [int][Math]::Ceiling($player.NaturalDuration.TimeSpan.TotalMilliseconds) + 250
  }
  Start-Sleep -Milliseconds $waitMs
} finally {
  if ($player) {
    $player.Stop()
    $player.Close()
  }
}

exit 0
