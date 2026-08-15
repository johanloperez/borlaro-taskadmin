using Microsoft.AspNetCore.SignalR;
using TaskAdmin.Infrastructure.Realtime;

namespace TaskAdmin.Api.Realtime;

/// <summary>Difunde los cambios del tablero por SignalR, al grupo del proyecto.
///
/// Vive acá porque es el único proyecto que conoce el hub; el dominio solo ve
/// <see cref="IBoardEvents"/>. Es el mismo arreglo que <see cref="DesktopChannel"/>.</summary>
public class SignalRBoardEvents(IHubContext<AgentHub> hub) : IBoardEvents
{
    public Task PublishAsync(BoardEvent evento, CancellationToken ct = default) =>
        hub.Clients.Group(AgentHub.BoardGroup(evento.ProjectKey)).SendAsync(
            "board",
            new
            {
                projectKey = evento.ProjectKey,
                itemId = evento.ItemId,
                itemKey = evento.ItemKey,
                kind = evento.Kind,
                byAgent = evento.ByAgent
            },
            ct);
}
