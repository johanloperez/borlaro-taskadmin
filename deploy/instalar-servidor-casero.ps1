# Deja esta maquina lista para servir TaskAdmin de forma permanente: respaldo diario, sin
# suspension, y Docker arrancando solo.
#
# Pensado para el caso "el servidor es mi PC y sale por un tunel de Cloudflare". No toca el
# tunel: eso se configura aparte porque depende de que dominio uses (ver el README).
#
# Hay que correrlo COMO ADMINISTRADOR.
#
#   .\instalar-servidor-casero.ps1
#   .\instalar-servidor-casero.ps1 -Hora "02:30" -Destino "D:\Respaldos" -Dias 30
#
# Todo lo que hace es reversible y se explica al final.

[CmdletBinding()]
param(
    [string]$Hora = "03:00",
    [string]$Destino = "C:\Respaldos\TaskAdmin",
    [int]$Dias = 14,
    [string]$Tarea = "TaskAdmin - Respaldo diario"
)

$ErrorActionPreference = "Stop"

$esAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $esAdmin) {
    Write-Host "Hay que correr esto como administrador." -ForegroundColor Red
    Write-Host "Abri PowerShell con boton derecho > 'Ejecutar como administrador' y repetilo."
    exit 1
}

$raiz = Split-Path -Parent $PSScriptRoot
$script = Join-Path $PSScriptRoot "respaldo.ps1"

if (-not (Test-Path $script)) {
    Write-Host "No encuentro $script" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== 1. Respaldo diario ===" -ForegroundColor Cyan

# La tarea corre como el usuario que la instala y SOLO con sesion iniciada. No es una limitacion
# que elegimos: Docker Desktop levanta su daemon dentro de la sesion del usuario, asi que una
# tarea corriendo como SYSTEM no encuentra el pipe y falla todas las noches en silencio.
# La consecuencia es que la maquina tiene que quedar con sesion iniciada, que es lo mismo que ya
# necesita Docker Desktop.
$usuario = "$env:USERDOMAIN\$env:USERNAME"

$accion = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument ("-NoProfile -ExecutionPolicy Bypass -File `"$script`" " +
               "-Destino `"$Destino`" -Dias $Dias") `
    -WorkingDirectory $raiz

$disparador = New-ScheduledTaskTrigger -Daily -At $Hora

$opciones = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopOnIdleEnd `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -MultipleInstances IgnoreNew

# StartWhenAvailable es lo que hace que un respaldo que se perdio porque la maquina estaba
# apagada se dispare al prender, en vez de saltearse el dia entero.

if (Get-ScheduledTask -TaskName $Tarea -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $Tarea -Confirm:$false
    Write-Host "  (reemplazando la tarea anterior)"
}

Register-ScheduledTask `
    -TaskName $Tarea `
    -Action $accion `
    -Trigger $disparador `
    -Settings $opciones `
    -User $usuario `
    -RunLevel Highest `
    -Description "Volcado de Postgres y de los archivos subidos de TaskAdmin, con retencion de $Dias dias." | Out-Null

Write-Host "  Tarea '$Tarea' registrada: todos los dias a las $Hora" -ForegroundColor Green
Write-Host "  Destino: $Destino (retiene $Dias dias)"

Write-Host "`n=== 2. Que no se suspenda ===" -ForegroundColor Cyan

# Un servidor que se duerme no es un servidor. La pantalla si puede apagarse: no afecta nada y
# alarga la vida del panel.
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0
powercfg /change disk-timeout-ac 0
powercfg /change monitor-timeout-ac 15
Write-Host "  Suspension, hibernacion y apagado de discos: desactivados (con corriente)" -ForegroundColor Green
Write-Host "  Pantalla: se apaga a los 15 minutos"

# Es una notebook (Ryzen 6900HX): cerrar la tapa la duerme y se corta el servicio.
try {
    powercfg /setacvalueindex SCHEME_CURRENT 4f971e89-eebd-4455-a8de-9e59040e7347 `
        5ca83367-6e45-459f-a27b-476b1d01c936 0
    powercfg /setactive SCHEME_CURRENT
    Write-Host "  Cerrar la tapa ya no la suspende (con corriente)" -ForegroundColor Green
}
catch {
    Write-Host "  No pude cambiar la accion de la tapa (puede que no aplique en este equipo)" -ForegroundColor Yellow
}

Write-Host "`n=== 3. Docker al arrancar ===" -ForegroundColor Cyan

$dockerExe = "C:\Program Files\Docker\Docker\Docker Desktop.exe"
$arranque = [Environment]::GetFolderPath("Startup")
$acceso = Join-Path $arranque "Docker Desktop.lnk"

if (-not (Test-Path $dockerExe)) {
    Write-Host "  No encontre Docker Desktop; salteado." -ForegroundColor Yellow
}
elseif (Test-Path $acceso) {
    Write-Host "  Ya estaba configurado para arrancar solo." -ForegroundColor Green
}
else {
    $sh = New-Object -ComObject WScript.Shell
    $lnk = $sh.CreateShortcut($acceso)
    $lnk.TargetPath = $dockerExe
    $lnk.Save()
    Write-Host "  Docker Desktop va a arrancar con la sesion." -ForegroundColor Green
}

Write-Host "`n=== Listo ===" -ForegroundColor Cyan
Write-Host @"

Que quedo hecho:
  - Respaldo diario a las $Hora en $Destino, reteniendo $Dias dias.
  - La maquina no se suspende ni apaga discos mientras este enchufada.
  - Docker Desktop arranca con la sesion.

Probar el respaldo ahora, sin esperar a manana:
  Start-ScheduledTask -TaskName "$Tarea"
  Get-Content "$Destino\respaldo.log" -Tail 15

Para deshacer todo:
  Unregister-ScheduledTask -TaskName "$Tarea" -Confirm:`$false
  powercfg /change standby-timeout-ac 30
  Remove-Item "$acceso"

Lo que ESTO NO hace y hay que mirar aparte:
  - El tunel de Cloudflare (necesita tu dominio). Ver el README.
  - Sacar una copia de los respaldos FUERA de esta maquina. Un respaldo en el mismo disco que
    la base no protege del unico escenario que importa: que ese disco muera.
  - Los reinicios de Windows Update. Configurar las horas activas para que no reinicie a mitad
    de la jornada.

"@
