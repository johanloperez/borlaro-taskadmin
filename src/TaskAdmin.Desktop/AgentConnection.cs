using Microsoft.AspNetCore.SignalR.Client;

namespace TaskAdmin.Desktop;

public record IncomingNotification(string Kind, string Title, string Body, string? LinkPath, Guid? CheckInId);

/// <summary>Conexión persistente con el servidor. Dos trabajos: recibir notificaciones y
/// mantener vivo el heartbeat, que es lo que le dice al servidor que este canal existe.</summary>
public class AgentConnection : IAsyncDisposable
{
    private readonly AppSettings _settings;
    private readonly HubConnection _connection;
    private readonly System.Timers.Timer _heartbeat;

    /// <summary>Distingue «se cayó la red» de «la persona cerró la sesión». Solo lo primero se
    /// reintenta.</summary>
    private bool _cerradoAdrede;

    public event Action<IncomingNotification>? NotificationReceived;
    public event Action<bool>? ConnectionStateChanged;

    public AgentConnection(AppSettings settings)
    {
        _settings = settings;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{settings.ServerUrl}/hubs/agent?access_token={settings.AccessToken}")
            // Reconexión automática con backoff: la app tiene que sobrevivir a una caída de red
            // o a que el equipo se suspenda, sin que nadie la reinicie a mano.
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(2)
            })
            .Build();

        _connection.On<IncomingNotification>("notification", n => NotificationReceived?.Invoke(n));

        _connection.Reconnected += _ => { ConnectionStateChanged?.Invoke(true); return Task.CompletedTask; };
        _connection.Reconnecting += _ => { ConnectionStateChanged?.Invoke(false); return Task.CompletedTask; };
        _connection.Closed += async _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            if (_cerradoAdrede) return;

            // WithAutomaticReconnect se rinde después del último intervalo; a partir de ahí
            // reintentamos nosotros, para siempre.
            await Task.Delay(TimeSpan.FromSeconds(30));
            if (_cerradoAdrede) return;

            await TryConnectAsync();
        };

        _heartbeat = new System.Timers.Timer(60_000);
        _heartbeat.Elapsed += async (_, _) => await SendHeartbeatAsync();
    }

    public bool IsConnected => _connection.State == HubConnectionState.Connected;

    public async Task<bool> TryConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.AccessToken)) return false;

        // Conectar a pedido levanta el cierre deliberado: es lo que pasa cuando entra el usuario
        // siguiente. Sin esto, el canal quedaba mudo hasta reiniciar la app.
        _cerradoAdrede = false;

        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
            }

            ConnectionStateChanged?.Invoke(true);
            await SendHeartbeatAsync();
            _heartbeat.Start();
            return true;
        }
        catch
        {
            ConnectionStateChanged?.Invoke(false);
            return false;
        }
    }

    private async Task SendHeartbeatAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(_settings.DeviceToken)) return;

        try
        {
            var version = typeof(AgentConnection).Assembly.GetName().Version?.ToString() ?? "0.0";
            await _connection.InvokeAsync("Heartbeat", _settings.DeviceToken, version);
        }
        catch
        {
            // Un latido perdido no es un problema: la tolerancia del servidor cubre tres.
        }
    }

    public async Task AcknowledgeDeliveryAsync(Guid checkInId)
    {
        if (!IsConnected) return;
        try { await _connection.InvokeAsync("AcknowledgeDelivery", checkInId); } catch { }
    }

    public async Task AcknowledgeOpenedAsync(Guid checkInId)
    {
        if (!IsConnected) return;
        try { await _connection.InvokeAsync("AcknowledgeOpened", checkInId); } catch { }
    }

    /// <summary>Corta el canal a propósito, al cambiar de usuario.
    ///
    /// Hace falta la bandera: el manejador de `Closed` reintenta para siempre, que es lo correcto
    /// cuando se cae la red y lo contrario de lo que se quiere acá. Sin esto, treinta segundos
    /// después de cerrar la sesión el canal volvería solo, latiendo con el token de la persona
    /// que se acaba de ir.</summary>
    public async Task DisconnectAsync()
    {
        _cerradoAdrede = true;
        _heartbeat.Stop();

        try { await _connection.StopAsync(); } catch { }

        ConnectionStateChanged?.Invoke(false);
    }

    public async ValueTask DisposeAsync()
    {
        _cerradoAdrede = true;
        _heartbeat.Stop();
        _heartbeat.Dispose();
        await _connection.DisposeAsync();
    }
}
