#requires -version 5
# Windows installer for ai-notify (PowerShell port of install.sh).
#
# Merges the agent hook configs into your user config and renders the [COMMAND_PATH]
# placeholder to this repo's location. No external tools (jq, xdotool, sound players)
# are needed on Windows: JSON merging is native and notify-if-unfocused.ps1 uses the
# Win32 API plus built-in sounds.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\install.ps1                # prompts
#   powershell -ExecutionPolicy Bypass -File .\install.ps1 claude opencode
#   powershell -ExecutionPolicy Bypass -File .\install.ps1 all

[CmdletBinding()]
param([Parameter(ValueFromRemainingArguments = $true)] [string[]] $Targets)

$ErrorActionPreference = 'Stop'

# Absolute path to this repo. Forward slashes keep JSON escaping simple and work fine
# with PowerShell's -File argument.
$SRC = (Split-Path -Parent $MyInvocation.MyCommand.Path)
$SRC_FWD = $SRC -replace '\\', '/'

$HomeDir = if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }
$CLAUDE_DST = Join-Path $HomeDir '.claude\settings.json'
$CODEX_DST = Join-Path $HomeDir '.codex\hooks.json'
$OPENCODE_CONFIG_DST = Join-Path $HomeDir '.config\opencode\opencode.jsonc'
$OPENCODE_PLUGIN_DST = Join-Path $HomeDir '.config\opencode\plugin\notify.js'

# Render a template file with [COMMAND_PATH] replaced, returned as a string.
function Render([string]$path) {
  (Get-Content -LiteralPath $path -Raw) -replace '\[COMMAND_PATH\]', $SRC_FWD
}

function Backup([string]$f) {
  if (-not (Test-Path -LiteralPath $f)) { return }
  $stamp = Get-Date -Format 'yyyyMMddHHmmss'
  $b = "$f.bak.$stamp"
  Copy-Item -LiteralPath $f -Destination $b -Force
  Write-Host "  backed up existing $f -> $b"
}

function Ensure-Dir([string]$f) {
  $dir = Split-Path -Parent $f
  if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
  }
}

function Write-Utf8NoBom([string]$path, [string]$content) {
  $encoding = New-Object System.Text.UTF8Encoding $false
  [System.IO.File]::WriteAllText($path, $content, $encoding)
}

# Pretty-print an object as JSON with 2-space indentation. PowerShell 5.1's built-in
# ConvertTo-Json aligns nested values under their key column, which looks ragged; this
# emits clean, fixed-width indentation instead. Scalars are delegated to ConvertTo-Json
# so string escaping and number/boolean formatting stay correct.
function ConvertTo-PrettyJson($Value, [int]$Indent = 0) {
  $pad = '  ' * $Indent
  $padInner = '  ' * ($Indent + 1)

  if ($null -eq $Value) { return 'null' }

  if ($Value -is [pscustomobject]) {
    $props = @($Value.PSObject.Properties)
    if ($props.Count -eq 0) { return '{}' }
    $lines = foreach ($p in $props) {
      $key = $p.Name | ConvertTo-Json -Compress
      $val = ConvertTo-PrettyJson $p.Value ($Indent + 1)
      "$padInner${key}: $val"
    }
    return "{`n" + ($lines -join ",`n") + "`n$pad}"
  }

  if ($Value -is [System.Collections.IDictionary]) {
    $keys = @($Value.Keys)
    if ($keys.Count -eq 0) { return '{}' }
    $lines = foreach ($k in $keys) {
      $key = "$k" | ConvertTo-Json -Compress
      $val = ConvertTo-PrettyJson $Value[$k] ($Indent + 1)
      "$padInner${key}: $val"
    }
    return "{`n" + ($lines -join ",`n") + "`n$pad}"
  }

  # Arrays / other enumerables (strings are IEnumerable too, so exclude them here).
  if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
    $items = @($Value)
    if ($items.Count -eq 0) { return '[]' }
    $lines = foreach ($item in $items) {
      "$padInner$(ConvertTo-PrettyJson $item ($Indent + 1))"
    }
    return "[`n" + ($lines -join ",`n") + "`n$pad]"
  }

  return ($Value | ConvertTo-Json -Compress)
}

# Deep-merge two objects parsed from JSON; values from $overlay win. Objects merge
# recursively; scalars and arrays are replaced (matches jq's `.[0] * .[1]`).
function Merge-Json($base, $overlay) {
  if (($base -is [pscustomobject]) -and ($overlay -is [pscustomobject])) {
    $result = [ordered]@{}
    foreach ($p in $base.PSObject.Properties) { $result[$p.Name] = $p.Value }
    foreach ($p in $overlay.PSObject.Properties) {
      if ($result.Contains($p.Name)) {
        $result[$p.Name] = Merge-Json $result[$p.Name] $p.Value
      } else {
        $result[$p.Name] = $p.Value
      }
    }
    return [pscustomobject]$result
  }
  # Overlay wins for scalars and arrays. The unary comma keeps a single-element array
  # from being unwrapped to a scalar on return (a PowerShell gotcha that would turn a
  # one-entry hooks array like Claude's "Stop" into a bare object Claude ignores).
  if ($overlay -is [array]) { return ,$overlay }
  return $overlay
}

# Install a JSON config: deep-merge into an existing target (new keys win). If the target
# exists but is not valid JSON, leave it untouched and tell the user to merge manually.
function Install-Json([string]$src, [string]$dst) {
  Ensure-Dir $dst
  $rendered = Render $src
  $overlay = $rendered | ConvertFrom-Json

  if (Test-Path -LiteralPath $dst) {
    $existingRaw = Get-Content -LiteralPath $dst -Raw
    $existing = $null
    try { $existing = $existingRaw | ConvertFrom-Json } catch { $existing = $null }
    if ($null -eq $existing) {
      Write-Warning "  $dst is not valid JSON; leaving it untouched. Merge from $src manually."
      return
    }
    $merged = Merge-Json $existing $overlay
    Backup $dst
    Write-Utf8NoBom $dst ((ConvertTo-PrettyJson $merged) + "`n")
    Write-Host "  merged into $dst"
  } else {
    Write-Utf8NoBom $dst ((ConvertTo-PrettyJson $overlay) + "`n")
    Write-Host "  wrote $dst"
  }
}

# Install a plain file (no JSON merge): back up then overwrite.
function Install-File([string]$src, [string]$dst) {
  Ensure-Dir $dst
  Backup $dst
  Write-Utf8NoBom $dst (Render $src)
  Write-Host "  wrote $dst"
}

# Decide which agent configs to install. Honors args (e.g. `claude opencode` or `all`);
# otherwise prompts.
$DO_CLAUDE = $false; $DO_CODEX = $false; $DO_OPENCODE = $false
function Select-Targets([string[]]$choice) {
  if (-not $choice -or $choice.Count -eq 0) {
    Write-Host 'Which agent configs do you want to install?'
    Write-Host '  1) Claude Code'
    Write-Host '  2) Codex'
    Write-Host '  3) opencode'
    Write-Host '  4) All'
    $reply = Read-Host 'Choose (e.g. 1 3, or 4 for all) [4]'
    if ([string]::IsNullOrWhiteSpace($reply)) { $reply = '4' }
    $choice = $reply -split '[\s,]+'
  }

  foreach ($c in $choice) {
    switch -Regex ($c.Trim().ToLower()) {
      '^(1|claude|claude-code|claudecode)$' { $script:DO_CLAUDE = $true }
      '^(2|codex)$'                         { $script:DO_CODEX = $true }
      '^(3|opencode)$'                      { $script:DO_OPENCODE = $true }
      '^(4|all|a)$' { $script:DO_CLAUDE = $true; $script:DO_CODEX = $true; $script:DO_OPENCODE = $true }
      '^$' { }
      default { Write-Warning "  Ignoring unknown choice: $c" }
    }
  }

  if (-not ($DO_CLAUDE -or $DO_CODEX -or $DO_OPENCODE)) {
    Write-Host 'Nothing selected; defaulting to All.'
    $script:DO_CLAUDE = $true; $script:DO_CODEX = $true; $script:DO_OPENCODE = $true
  }
}

Write-Host "Installing ai-notify (Windows) from: $SRC"

Select-Targets $Targets

if ($DO_CLAUDE) {
  Write-Host 'Claude Code:'
  Install-Json (Join-Path $SRC 'templates\claude\settings.windows.json') $CLAUDE_DST
}

if ($DO_CODEX) {
  Write-Host 'Codex:'
  Install-Json (Join-Path $SRC 'templates\codex\hooks.windows.json') $CODEX_DST
}

if ($DO_OPENCODE) {
  Write-Host 'opencode:'
  # opencode.jsonc may contain comments, which the JSON parser cannot merge, so never
  # touch an existing one.
  if (Test-Path -LiteralPath $OPENCODE_CONFIG_DST) {
    Write-Host "  $OPENCODE_CONFIG_DST exists; leaving it untouched. Merge the 'tui.attention' block from $SRC\templates\opencode\opencode.jsonc manually."
  } else {
    Install-File (Join-Path $SRC 'templates\opencode\opencode.jsonc') $OPENCODE_CONFIG_DST
  }
  Install-File (Join-Path $SRC 'templates\opencode\plugin\notify.windows.js') $OPENCODE_PLUGIN_DST
}

Write-Host 'Done.'
