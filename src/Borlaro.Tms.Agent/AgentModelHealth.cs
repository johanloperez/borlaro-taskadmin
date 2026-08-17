using System.Collections.Concurrent;

namespace Borlaro.Tms.Agent;

/// <summary>El último fallo del proveedor de cada organización.
///
/// **El problema que resuelve.** Cuando el modelo rechaza un pedido —una clave vencida, un modelo
/// que no soporta herramientas— el error queda en el log del servidor, y quien está sentado
/// adelante ve «se cortó la conexión»: un mensaje que apunta a la red cuando el problema es la
/// configuración. Y quien podría arreglarlo está mirando la pantalla de Configuración, que hasta
/// ahora solo sabía decir qué estaba *configurado*, no qué había pasado al usarlo.
///
/// Vive en memoria y se pierde al reiniciar, a propósito: es un diagnóstico de «por qué no anda
/// ahora», no un registro de auditoría. Guardarlo en la base obligaría a decidir cuándo se limpia,
/// y un error viejo mostrado como actual confunde más que no mostrar nada. El historial de lo que
/// el agente hizo ya vive en `AgentActions`.</summary>
public class AgentModelHealth
{
    public record Falla(string Message, DateTimeOffset At);

    private readonly ConcurrentDictionary<Guid, Falla> _fallas = new();

    /// <summary>La clave de las organizaciones para los caminos que corren fuera de una — un
    /// barrido de fondo que todavía no entró en ninguna.</summary>
    private static readonly Guid SinOrganizacion = Guid.Empty;

    public void Registrar(Guid? organizationId, string mensaje) =>
        _fallas[organizationId ?? SinOrganizacion] = new(mensaje, DateTimeOffset.UtcNow);

    /// <summary>Se limpia cuando una conversación sale bien: mostrar el error de ayer sobre una
    /// configuración que ya se arregló manda a buscar un problema que no existe.</summary>
    public void Limpiar(Guid? organizationId) =>
        _fallas.TryRemove(organizationId ?? SinOrganizacion, out _);

    public Falla? Ultima(Guid? organizationId) =>
        _fallas.TryGetValue(organizationId ?? SinOrganizacion, out var f) ? f : null;
}
