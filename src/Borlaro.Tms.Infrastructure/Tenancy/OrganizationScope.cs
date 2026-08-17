namespace Borlaro.Tms.Infrastructure.Tenancy;

/// <summary>De qué organización es el trabajo que se está haciendo ahora mismo. El DbContext lo
/// consulta para filtrar toda lectura y para completar toda escritura.
///
/// Es ambiental (<see cref="AsyncLocal{T}"/>) y no un servicio inyectado por una razón concreta:
/// el filtro global de EF se compila una sola vez con el modelo, y una expresión que capturara
/// un servicio con ciclo de vida por request se quedaría apuntando para siempre a la primera
/// instancia que existió. Una referencia estática se evalúa en cada consulta y no tiene esa
/// trampa.
///
/// El estado por defecto —nadie llamó a <see cref="Use"/>— **no ve nada**. Es deliberado: si
/// aparece un camino que se olvidó de declarar su organización, el resultado es una lista vacía y
/// un error al guardar, no las filas de todas las empresas.</summary>
public static class OrganizationScope
{
    private sealed record State(Guid OrganizationId, bool IsSystem);

    private static readonly AsyncLocal<State?> Current = new();

    /// <summary>La organización activa, o <see cref="Guid.Empty"/> si no hay ninguna. El filtro
    /// compara contra esto, así que «ninguna» equivale a «ninguna fila».</summary>
    public static Guid CurrentId => Current.Value?.OrganizationId ?? Guid.Empty;

    /// <summary>Null cuando no hay organización activa. Para quien necesite distinguir «no hay»
    /// de <see cref="Guid.Empty"/> sin mirar el filtro.</summary>
    public static Guid? Organization =>
        Current.Value is { IsSystem: false, OrganizationId: var id } ? id : null;

    /// <summary>Falso solo en modo sistema. Lo lee el filtro global; es público porque la
    /// expresión que lo usa vive en el DbContext.</summary>
    public static bool FilterEnabled => Current.Value is not { IsSystem: true };

    public static bool IsSystem => Current.Value is { IsSystem: true };

    /// <summary>Entra en el contexto de una organización. Al liberar el resultado se vuelve al
    /// contexto anterior, que es lo que hace que anidar sea seguro.</summary>
    public static IDisposable Use(Guid organizationId) =>
        Enter(new State(organizationId, IsSystem: false));

    /// <summary>Desactiva el filtro: la consulta ve todas las organizaciones. Solo para procesos
    /// que por definición atraviesan la instalación —el barrido de check-ins, las migraciones, el
    /// arranque— y nunca para atender un request. Todo lo que se escriba adentro tiene que traer
    /// su <c>OrganizationId</c> explícito, porque acá no hay ninguno que estampar.</summary>
    public static IDisposable UseSystem() =>
        Enter(new State(Guid.Empty, IsSystem: true));

    private static IDisposable Enter(State state)
    {
        var previous = Current.Value;
        Current.Value = state;
        return new Restore(previous);
    }

    private sealed class Restore(State? previous) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            Current.Value = previous;
        }
    }
}
