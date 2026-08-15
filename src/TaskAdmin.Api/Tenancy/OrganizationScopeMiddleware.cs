using Microsoft.AspNetCore.SignalR;
using TaskAdmin.Api.Auth;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Api.Tenancy;

/// <summary>Abre el contexto de organización de cada request a partir del claim del token, y lo
/// cierra al terminar. A partir de acá, cualquier consulta que haga el request ve solo las filas
/// de esa organización, sin que ningún servicio ni endpoint tenga que acordarse de acotarla.
///
/// Va después de <c>UseAuthentication</c> —antes no hay claims que leer— y antes de las rutas.
/// Los caminos anónimos que sí tienen organización (el formulario público de intake, la vuelta
/// del proveedor de identidad) la resuelven por su propia vía, porque la suya no viaja en un
/// token.</summary>
public class OrganizationScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var organizationId = context.User.OrganizationId();

        if (organizationId is null)
        {
            await next(context);
            return;
        }

        using var scope = OrganizationScope.Use(organizationId.Value);
        await next(context);
    }
}

/// <summary>Lo mismo para el hub de SignalR. Hace falta aparte porque las invocaciones de un hub
/// no vuelven a pasar por el pipeline HTTP: el middleware corre en el handshake y nada más, así
/// que sin esto cada método del hub correría sin organización y no vería ni sus propias filas.</summary>
public class OrganizationHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var organizationId = invocationContext.Context.User?.OrganizationId();
        if (organizationId is null) return await next(invocationContext);

        using var scope = OrganizationScope.Use(organizationId.Value);
        return await next(invocationContext);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var organizationId = context.Context.User?.OrganizationId();
        if (organizationId is null)
        {
            await next(context);
            return;
        }

        using var scope = OrganizationScope.Use(organizationId.Value);
        await next(context);
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        var organizationId = context.Context.User?.OrganizationId();
        if (organizationId is null)
        {
            await next(context, exception);
            return;
        }

        using var scope = OrganizationScope.Use(organizationId.Value);
        await next(context, exception);
    }
}
