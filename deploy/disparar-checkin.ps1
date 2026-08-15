# Dispara un check-in ya vencido para una persona, sin esperar a su horario.
#
# Es la herramienta para probar el canal de notificaciones a mano: crea el check-in con el
# proximo peldano de la escalera vencido, asi el barrido lo despacha en el siguiente ciclo
# (unos pocos segundos) en vez de a la hora programada.
#
#   .\disparar-checkin.ps1 prueba@taskadmin.local
#   .\disparar-checkin.ps1 prueba@taskadmin.local -Rehacer
#
# -Rehacer borra el check-in de hoy de esa persona antes de crear el nuevo. Sin eso, si ya tenia
# uno, el indice unico (UserId, LocalDate) lo impide y no pasa nada.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Email,

    [switch]$Rehacer,
    [string]$Contenedor = "taskadmin-postgres"
)

$ErrorActionPreference = "Stop"

# El SQL va por entrada estandar y no como argumento: al pasar `-c "SELECT ""Id"""` PowerShell se
# come las comillas dobles al invocar un ejecutable nativo, y sin ellas Postgres baja los
# identificadores a minusculas y no encuentra las tablas en PascalCase.
function Consultar($sql) {
    $sql | docker exec -i $Contenedor psql -U taskadmin -d taskadmin -t -A -F' | '
}

$datos = Consultar "SELECT u.""Id"", u.""OrganizationId"", u.""Name"" FROM ""Users"" u WHERE lower(u.""Email"")=lower('$Email');"

if ([string]::IsNullOrWhiteSpace($datos)) {
    Write-Host "No existe ningun usuario con el email $Email" -ForegroundColor Red
    exit 1
}

$partes = $datos.Trim() -split ' \| '
$uid, $oid, $nombre = $partes[0], $partes[1], $partes[2]
Write-Host "Persona: $nombre" -ForegroundColor Cyan

# Sin trabajo asignado el agente abre, dice que no hay nada y cierra. Sirve para probar el globo,
# no para probar la conversacion; conviene avisarlo antes de que parezca que algo fallo.
$tareas = (Consultar "SELECT COUNT(*) FROM ""WorkItems"" WHERE ""AssigneeId""='$uid';").Trim()
if ($tareas -eq "0") {
    Write-Host "Aviso: no tiene tareas asignadas, asi que el agente no va a tener de que hablar." -ForegroundColor Yellow
}

if ($Rehacer) {
    Consultar "DELETE FROM ""CheckIns"" WHERE ""UserId""='$uid' AND ""LocalDate""=CURRENT_DATE;" | Out-Null
    Write-Host "Check-in de hoy borrado."
}

$id = [guid]::NewGuid()

# NextEscalationAt = now() es lo que lo hace inmediato: el barrido levanta lo que ya vencio.
# Status 'Pending' y OpenedAt nulo son las otras dos condiciones de la busqueda.
$sql = @"
INSERT INTO "CheckIns" ("Id","UserId","OrganizationId","ScheduledAt","LocalDate","Status",
                        "EscalationStep","NextEscalationAt","ClosedAsNoChanges","TurnCount",
                        "InputTokens","OutputTokens","CacheReadTokens","CreatedAt")
VALUES ('$id','$uid','$oid', now(), CURRENT_DATE, 'Pending','FirstDesktopToast', now(),
        false, 0,0,0,0, now())
ON CONFLICT ("UserId","LocalDate") DO NOTHING
RETURNING "Id";
"@

$creado = Consultar $sql

if ([string]::IsNullOrWhiteSpace($creado)) {
    Write-Host "Ya tenia un check-in hoy. Usa -Rehacer para reemplazarlo." -ForegroundColor Yellow
    exit 0
}

Write-Host "Check-in creado: $id" -ForegroundColor Green
Write-Host "El barrido lo despacha en unos segundos. Mirando la entrega..."

foreach ($i in 1..10) {
    Start-Sleep -Seconds 3
    $estado = (Consultar "SELECT ""Status"" || ' | ' || COALESCE(""DeliveredVia""::text,'sin entregar') FROM ""CheckIns"" WHERE ""Id""='$id';").Trim()
    Write-Host "  $estado"

    # DeliveredVia = Desktop solo lo escribe el acuse del cliente, asi que ver eso es la prueba
    # de que la app recibio el aviso, no solo de que el servidor lo mando.
    if ($estado -like "*Desktop*") {
        Write-Host "`nEntregado y ACUSADO por la app de escritorio." -ForegroundColor Green
        exit 0
    }
}

Write-Host "`nNo llego el acuse. Revisa que la app este corriendo y conectada." -ForegroundColor Yellow
