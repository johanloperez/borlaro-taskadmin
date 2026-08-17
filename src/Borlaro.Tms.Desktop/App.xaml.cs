using System.Drawing;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Borlaro.Tms.Desktop;

// Con UseWindowsForms activado hay dos tipos `Application` en scope; se califica el de WPF,
// que es del que hereda la app.
public partial class App : System.Windows.Application
{
    private const string MutexName = "Borlaro.Tms.Desktop.SingleInstance";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Borlaro TMS";

    private Mutex? _singleInstance;
    private WinForms.NotifyIcon? _tray;
    private MainWindow? _window;
    private AgentConnection? _connection;
    private AppSettings _settings = new();
    private Guid? _pendingCheckInId;

    /// <summary>A dónde lleva el último aviso. El chat se abre en la app; cualquier cosa que sea
    /// el sistema web —una tarea, el tablero, un entregable— se abre en el navegador.</summary>
    private string? _pendingLink;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Instancia única: dos apps latiendo con el mismo token de dispositivo confunden al
        // servidor sobre qué canal está vivo.
        _singleInstance = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // `OnStartup` es `async void`: lo que se escape después del primer `await` no lo atrapa
        // nadie y termina la app con el diálogo de error de Windows, antes de que llegue a
        // mostrar una ventana. Para una app que vive en la bandeja eso se ve como «no arrancó»,
        // sin ninguna pista de por qué.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            ReportarFalla(args.Exception);
        };

        // Y esto para lo que falle en una tarea de fondo —el canal en tiempo real, el acuse de
        // entrega— que si no termina el proceso sin pasar por el manejador de arriba.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
        };

        try
        {
            await ArrancarAsync(e.Args);
        }
        catch (Exception ex)
        {
            ReportarFalla(ex);
        }
    }

    private async Task ArrancarAsync(string[] args)
    {
        _settings = AppSettings.Load();
        EnsureAutoStart();
        SetupTray();

        _window = new MainWindow(_settings);
        _window.HideRequested += () => ShowBalloon("Borlaro TMS sigue activo", "Lo encontrás en la bandeja del sistema.");

        _connection = new AgentConnection(_settings);
        _connection.NotificationReceived += OnNotification;
        _connection.ConnectionStateChanged += OnConnectionStateChanged;

        // Al entrar desde la ventana, se conecta el canal sin reiniciar nada.
        _window.SignedIn += async () => await _connection.TryConnectAsync();

        // La sesión se renueva sola con el token del equipo: la contraseña se pide una vez, la
        // primera. Si el equipo fue revocado desde la web, ahí sí vuelve a pedirla.
        if (await _window.TryRestoreSessionAsync())
        {
            await _connection.TryConnectAsync();

            // Normalmente arranca escondida en la bandeja: es una app de avisos, y aparecer sola
            // en cada inicio de sesión de Windows sería una molestia diaria. Con `--chat` abre el
            // check-in directo, que es lo que hace falta para un acceso directo o para probarla.
            if (args.Any(a => a.Equals("--chat", StringComparison.OrdinalIgnoreCase)))
            {
                await _window.AbrirCheckInAsync();
            }
        }
        else
        {
            _window.ShowLogin();
        }
    }

    /// <summary>Cuenta qué pasó en vez de morir en silencio, y deja rastro en disco.
    ///
    /// El archivo importa: los errores de arranque de una app de bandeja no los ve nadie —no hay
    /// consola, y la ventana todavía no existe—, así que sin esto la única información disponible
    /// era «no anda».</summary>
    private void ReportarFalla(Exception ex)
    {
        try
        {
            var carpeta = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Borlaro TMS");
            Directory.CreateDirectory(carpeta);

            File.AppendAllText(
                System.IO.Path.Combine(carpeta, "errores.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Si no se puede ni escribir el log, igual hay que mostrar el mensaje.
        }

        // Calificado: con UseWindowsForms activado hay dos MessageBox en scope.
        System.Windows.MessageBox.Show(
            $"Borlaro TMS tuvo un problema y no pudo seguir.\n\n{ex.Message}\n\n" +
            "El detalle quedó en:\n%APPDATA%\\Borlaro TMS\\errores.log",
            "Borlaro TMS", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>Cómo se llamaba la entrada de autoarranque antes del renombre a Borlaro TMS.
    ///
    /// Se borra al arrancar, y no es limpieza cosmética: la entrada vieja apunta a
    /// `TaskAdmin.exe`, un binario que dejó de existir cuando el proyecto pasó a llamarse
    /// `Borlaro.Tms.Desktop` y el ejecutable a `BorlaroTms.exe`. Windows la ejecuta igual en cada
    /// inicio de sesión y falla con un error que parece de la aplicación —y no lo es, porque la
    /// aplicación buena arranca al lado desde la entrada nueva.</summary>
    private const string LegacyRunValueName = "TaskAdmin";

    /// <summary>Autoarranque por la clave Run del usuario: no requiere permisos de
    /// administrador, a diferencia de un servicio o de la rama HKLM.</summary>
    private void EnsureAutoStart()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is null) return;

            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(RunValueName) as string != exePath)
            {
                key?.SetValue(RunValueName, exePath);
            }

            // Solo si existe: DeleteValue tira si el nombre no está, y acá una excepción se
            // comería el registro del autoarranque que acabamos de escribir.
            if (key?.GetValue(LegacyRunValueName) is not null)
            {
                key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Si una política de la empresa bloquea la escritura en el registro, la app sigue
            // sirviendo mientras esté abierta. No es motivo para no arrancar.
        }
    }

    private void SetupTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Borlaro TMS"
        };

        // Doble click abre lo único que la app sabe hacer: el chat. El tablero no vive acá.
        _tray.DoubleClick += (_, _) => AbrirCheckIn();

        // El clic en el globo abre el check-in si el aviso es sobre uno, y si no, manda al
        // navegador: un entregable o una aprobación se revisan allá.
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_pendingCheckInId is not null || _pendingLink is null) AbrirCheckIn();
            else AbrirEnNavegador(_pendingLink);
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Check-in ahora", null, (_, _) => AbrirCheckIn());
        menu.Items.Add("Mensajes", null, (_, _) => AbrirEnNavegador("/mensajes"));
        menu.Items.Add("Ir al sistema web", null, (_, _) => AbrirEnNavegador("/"));
        menu.Items.Add(new WinForms.ToolStripSeparator());

        var pauseItem = new WinForms.ToolStripMenuItem("Pausar notificaciones")
        {
            CheckOnClick = true,
            Checked = _settings.NotificationsPaused
        };
        pauseItem.CheckedChanged += (_, _) =>
        {
            _settings.NotificationsPaused = pauseItem.Checked;
            _settings.Save();
        };
        menu.Items.Add(pauseItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // «Cambiar de usuario» y no «Cerrar sesión»: en una app que vive en la bandeja, «cerrar»
        // se lee como apagarla, que es lo que hace el ítem de abajo.
        menu.Items.Add("Cambiar de usuario", null, (_, _) =>
        {
            _window?.SignOut();
            _ = _connection?.DisconnectAsync();
        });

        menu.Items.Add("Salir", null, (_, _) => Shutdown());

        _tray.ContextMenuStrip = menu;
    }

    private async void OnNotification(IncomingNotification notification)
    {
        _pendingCheckInId = notification.CheckInId;

        // A dónde lleva esta notificación si la persona hace clic. Sin esto el globo era
        // informativo y nada más: contaba que algo pasó y dejaba a la persona buscándolo a mano.
        _pendingLink = notification.LinkPath;

        // El acuse de entrega se manda apenas llega, y es distinto del de apertura: "el toast
        // salió" no es "la persona lo vio", y la escalera solo se detiene con el segundo.
        if (notification.CheckInId is Guid id && _connection is not null)
        {
            await _connection.AcknowledgeDeliveryAsync(id);
        }

        if (_settings.NotificationsPaused) return;

        Dispatcher.Invoke(() => ShowBalloon(notification.Title, notification.Body));
    }

    private void OnConnectionStateChanged(bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            if (_tray is not null)
            {
                _tray.Text = connected ? "Borlaro TMS — conectado" : "Borlaro TMS — reconectando…";
            }
        });
    }

    private void ShowBalloon(string title, string body)
    {
        _tray?.ShowBalloonTip(10_000, title, body, WinForms.ToolTipIcon.Info);
    }

    /// <summary>Abre el check-in en la ventana. Es lo único que la ventana sabe hacer.</summary>
    private async void AbrirCheckIn()
    {
        if (_window is null) return;

        await _window.AbrirCheckInAsync(_pendingCheckInId);

        // Abrir la ventana del check-in ES el acuse de apertura: es el momento exacto en que
        // la persona lo vio, y lo que detiene la escalera.
        if (_pendingCheckInId is Guid id && _connection is not null)
        {
            await _connection.AcknowledgeOpenedAsync(id);
        }
    }

    /// <summary>Lo que no es el check-in se abre en el navegador, donde ya vive.</summary>
    private void AbrirEnNavegador(string path) => _window?.OpenInBrowser(path);

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        if (_connection is not null) await _connection.DisposeAsync();
        _singleInstance?.Dispose();

        base.OnExit(e);
    }
}
