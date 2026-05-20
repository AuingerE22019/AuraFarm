# Full reset: wipe Postgres volume, re-init mock data, restart API + Angular.
# Run from repo root in PowerShell.

$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }
Set-Location $root

function Stop-PortListener([int]$Port) {
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conns) {
        $procId = $c.OwningProcess
        if ($procId -and $procId -ne 0) {
            Write-Host "Stopping process $procId on port $Port..." -ForegroundColor DarkYellow
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "Stopping API / frontend..." -ForegroundColor Cyan
Get-Process -Name "AuraFarm.Api" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Stop-PortListener 5023
Stop-PortListener 4200
Start-Sleep -Seconds 1

Write-Host "Wiping database volume and re-initializing mock data..." -ForegroundColor Cyan
docker compose down -v
docker compose up -d

Write-Host "Waiting for Postgres..." -ForegroundColor Cyan
$ready = $false
for ($i = 0; $i -lt 45; $i++) {
    $hc = docker inspect --format='{{.State.Health.Status}}' aurafarm-db 2>$null
    if ($hc -eq 'healthy') { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if (-not $ready) { Write-Host "Postgres healthcheck timeout - continuing anyway." -ForegroundColor Yellow }

Write-Host "Starting API (http://localhost:5023) in new window..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$root\backend\AuraFarm.Api'; dotnet run --launch-profile http"

Start-Sleep -Seconds 3

Write-Host "Starting Angular (http://localhost:4200) in new window..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$root\frontend\aura-farm-web'; npx ng serve --port 4200 --host localhost"

Write-Host "Reset complete." -ForegroundColor Green
Write-Host "Staff: http://localhost:4200/staff/login  (Admin / password)" -ForegroundColor Green
Write-Host "Member: register via staff, then http://localhost:4200/login?setup=true" -ForegroundColor Green
