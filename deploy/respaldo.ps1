# Respaldo diario de TaskAdmin: la base y los archivos subidos.
#
# Son dos cosas distintas y las dos hacen falta. La base sin los entregables deja tarjetas que
# apuntan a archivos que no existen; los archivos sin la base son una carpeta de nombres sueltos.
#
# Se respalda desde adentro de los contenedores a proposito: `pg_dump` corre en el contenedor de
# Postgres (asi no hace falta instalar el cliente en Windows, ni que coincidan las versiones) y
# los archivos salen por un contenedor descartable que monta el volumen (asi funciona sin saber
# donde guarda Docker sus volumenes, que cambia entre Docker Desktop y Linux).
#
# Uso:
#   .\respaldo.ps1                          -> respalda en C:\Respaldos\TaskAdmin
#   .\respaldo.ps1 -Destino D:\copias -Dias 30
#
# Devuelve codigo distinto de cero si algo fallo, para que el Programador de tareas lo marque
# como error en vez de mostrar un exito silencioso sobre un respaldo vacio.

[CmdletBinding()]
param(
    [string]$Destino = "C:\Respaldos\TaskAdmin",
    [int]$Dias = 14
)

$ErrorActionPreference = "Stop"
$sello = Get-Date -Format "yyyy-MM-dd_HHmm"
$fallos = @()

function Escribir($texto) {
    $linea = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $texto
    Write-Host $linea
    Add-Content -Path (Join-Path $Destino "respaldo.log") -Value $linea -Encoding utf8
}

if (-not (Test-Path $Destino)) {
    New-Item -ItemType Directory -Path $Destino -Force | Out-Null
}

Escribir "=== Respaldo $sello ==="

# Sin daemon no hay nada que hacer, y conviene decirlo claro: es la causa mas probable de que un
# respaldo programado no haya corrido en semanas.
try { docker ps | Out-Null } catch { }
if ($LASTEXITCODE -ne 0) {
    Escribir "ERROR: Docker no responde. No se respaldo nada."
    exit 1
}

# --- La base -----------------------------------------------------------------------------------
# --format=custom en vez de SQL plano: permite restaurar una tabla sola y comprime de fabrica.
$archivoBase = Join-Path $Destino "taskadmin_$sello.dump"
Escribir "Volcando la base..."
try {
    docker exec taskadmin-postgres pg_dump -U taskadmin -d taskadmin --format=custom --file=/tmp/respaldo.dump
    if ($LASTEXITCODE -ne 0) { throw "pg_dump devolvio $LASTEXITCODE" }

    docker cp taskadmin-postgres:/tmp/respaldo.dump $archivoBase
    if ($LASTEXITCODE -ne 0) { throw "docker cp devolvio $LASTEXITCODE" }

    docker exec taskadmin-postgres rm -f /tmp/respaldo.dump | Out-Null

    $mb = [math]::Round((Get-Item $archivoBase).Length / 1MB, 2)
    if ($mb -eq 0) { throw "el volcado quedo vacio" }
    Escribir "  base OK: $mb MB"
}
catch {
    Escribir "  ERROR en la base: $_"
    $fallos += "base"
}

# --- Los archivos subidos ----------------------------------------------------------------------
# Un contenedor descartable monta el volumen y escribe el tar directo en la carpeta destino.
#
# Escribe adentro y no por la salida estandar a proposito: PowerShell 5.1 decodifica como texto
# lo que sale de un ejecutable nativo, asi que `docker ... | Set-Content` corrompe el .tar.gz sin
# avisar. El respaldo parece correcto hasta el dia que hay que restaurarlo.
$archivoSubidos = Join-Path $Destino "uploads_$sello.tar.gz"
Escribir "Empaquetando los archivos subidos..."
try {
    docker run --rm -v taskadmin_uploads:/datos:ro -v "${Destino}:/salida" alpine `
        tar czf "/salida/uploads_$sello.tar.gz" -C /datos .
    if ($LASTEXITCODE -ne 0) { throw "tar devolvio $LASTEXITCODE" }

    # Se verifica que el tar se pueda leer. Un respaldo que no se prueba no es un respaldo.
    docker run --rm -v "${Destino}:/salida:ro" alpine tar tzf "/salida/uploads_$sello.tar.gz" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "el tar quedo ilegible" }

    $mb = [math]::Round((Get-Item $archivoSubidos).Length / 1MB, 2)
    Escribir "  archivos OK y verificados: $mb MB"
}
catch {
    Escribir "  ERROR en los archivos: $_"
    $fallos += "archivos"
}

# --- Retencion ---------------------------------------------------------------------------------
# Se borra por edad y solo lo que este script genera. El patron es explicito para que un archivo
# que alguien haya dejado a mano en esa carpeta no desaparezca solo.
$limite = (Get-Date).AddDays(-$Dias)
$viejos = Get-ChildItem -Path $Destino -File |
    Where-Object { $_.Name -match '^(taskadmin_|uploads_)' -and $_.LastWriteTime -lt $limite }

foreach ($v in $viejos) {
    Remove-Item $v.FullName -Force
    Escribir "  borrado por antiguedad: $($v.Name)"
}

# --- Cierre ------------------------------------------------------------------------------------
$total = [math]::Round((Get-ChildItem $Destino -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Escribir "Carpeta: $total MB en total, reteniendo $Dias dias."

if ($fallos.Count -gt 0) {
    Escribir "=== TERMINO CON ERRORES: $($fallos -join ', ') ==="
    exit 1
}

Escribir "=== Respaldo completo ==="
exit 0
