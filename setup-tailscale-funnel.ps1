#Requires -Version 5.1
<#
.SYNOPSIS
  Постоянный Hub URL через Tailscale Funnel (без роутера и Cloudflare).

  1. Установите Tailscale: https://tailscale.com/download/windows
  2. Запустите этот скрипт — войдите в аккаунт, если попросит.
  3. URL сохранится в %AppData%\ConfigAdmin\sync-tunnel.url
#>
$ErrorActionPreference = "Stop"

$Tailscale = "${env:ProgramFiles}\Tailscale\tailscale.exe"
$HubUrlFile = Join-Path $env:APPDATA "ConfigAdmin\sync-tunnel.url"

if (-not (Test-Path $Tailscale)) {
    Write-Host "Tailscale не установлен." -ForegroundColor Red
    Write-Host "Скачайте: https://tailscale.com/download/windows"
    Write-Host "Или: Invoke-WebRequest https://pkgs.tailscale.com/stable/tailscale-setup-latest.exe -OutFile `$env:TEMP\ts.exe; Start-Process `$env:TEMP\ts.exe"
    exit 1
}

$statusJson = & $Tailscale status --json 2>$null | ConvertFrom-Json
if (-not $statusJson.Self.Online -or $statusJson.BackendState -eq "NeedsLogin") {
    Write-Host "=== Вход в Tailscale ===" -ForegroundColor Cyan
    Write-Host "Откроется браузер. Войдите (Google/Microsoft/GitHub или email)."
    & $Tailscale login
    Start-Sleep -Seconds 2
    $statusJson = & $Tailscale status --json | ConvertFrom-Json
    if ($statusJson.BackendState -eq "NeedsLogin") {
        Write-Error "Вход не завершён. Запустите скрипт снова после login."
    }
}

Write-Host ""
Write-Host "=== Tailscale Funnel :18443 ===" -ForegroundColor Cyan
Write-Host "ConfigAdmin должен работать в режиме Админка (receiver на порту 18443)."
Write-Host ""

# Остановить предыдущий funnel если был
& $Tailscale funnel reset 2>$null | Out-Null

$funnelText = & $Tailscale funnel --bg 18443 2>&1 | Out-String
Write-Host $funnelText

$hubUrl = $null
if ($funnelText -match "(https://[^\s\]]+\.ts\.net)") {
    $hubUrl = $Matches[1].TrimEnd('/')
}
else {
    # fallback: DNS name из status
    $dns = $statusJson.Self.DNSName
    if ($dns) {
        $hubUrl = "https://$($dns.TrimEnd('.'))"
        Write-Host "Funnel URL не распознан из вывода. Попробуйте: tailscale funnel 18443"
        Write-Host "Или базовый hostname: $hubUrl"
    }
}

if ($hubUrl) {
    New-Item -ItemType Directory -Force -Path (Split-Path $HubUrlFile) | Out-Null
    Set-Content -Path $HubUrlFile -Value $hubUrl -Encoding UTF8
    Write-Host ""
    Write-Host "=== Постоянный Hub URL ===" -ForegroundColor Green
    Write-Host "  $hubUrl" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Сохранено: $HubUrlFile"
    Write-Host "Укажите этот URL в RDP-узле и в Передатчике на RDP."
    Write-Host ""
    Write-Host "Повторный запуск tunnel: start-sync-tunnel.bat"
}
