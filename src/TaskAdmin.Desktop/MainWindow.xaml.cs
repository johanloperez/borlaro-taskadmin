using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace TaskAdmin.Desktop;

/// <summary>La ventana de la app hace dos cosas y nada más: el chat del agente y las
/// notificaciones. No replica el tablero, ni el detalle de tareas, ni la configuración.
///
/// **El chat se dibuja acá, contra la API.** Antes se embebía la aplicación web en un WebView2 y
/// eso era un error de fondo: la web trae su propio router y su propio guardia de sesión, así que
/// cuando la sesión inyectada no le alcanzaba, redirigía al login **adentro** de esta ventana.
/// Una persona que ya había entrado en la app se encontraba con una segunda pantalla de entrada,
/// y no había forma de evitarlo desde afuera sin perseguir cada cambio de la web.
///
/// Dibujarlo nativo son doscientas líneas y un contrato explícito: turnos que entran, texto que
/// sale. Todo lo que no es el check-in —el tablero, una tarea, un entregable— se abre en el
/// navegador del sistema, que es donde ya vive y donde está bien.</summary>
public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly HttpClient _http = new();
    private readonly AgentApi _api;

    private readonly ObservableCollection<ChatBubble> _turnos = [];
    private Guid? _checkInId;
    private bool _ocupado;

    /// <summary>Se dispara cuando el usuario cierra la ventana. La app no termina: se esconde
    /// en la bandeja, porque cerrarla dejaría a la persona sin canal de escritorio sin que se
    /// dé cuenta.</summary>
    public event Action? HideRequested;

    /// <summary>Se dispara cuando la persona entra. La app usa la sesión para el canal en tiempo
    /// real, que es lo que hace que lleguen los avisos.</summary>
    public event Action? SignedIn;

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _api = new AgentApi(settings);

        TurnsList.ItemsSource = _turnos;
        ServerBox.Text = settings.ServerUrl;
        EmailBox.Text = settings.Email ?? string.Empty;
    }

    public string WebUrl(string path) => $"{_settings.WebUrl.TrimEnd('/')}{path}";

    /// <summary>Abre una ruta en el navegador del sistema. Es la salida para todo lo que excede
    /// al chat: revisar un entregable, aprobar algo, ver el tablero.</summary>
    public void OpenInBrowser(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(WebUrl(path)) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"No se pudo abrir el navegador.\n\nEntrá a mano a:\n{WebUrl(path)}\n\n{ex.Message}",
                "TaskAdmin", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ── Estados de la ventana ────────────────────────────────────────────────────────────────

    private void Mostrar(FrameworkElement panel)
    {
        ChatPanel.Visibility = ReferenceEquals(panel, ChatPanel) ? Visibility.Visible : Visibility.Collapsed;
        LoginPanel.Visibility = ReferenceEquals(panel, LoginPanel) ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Visibility = ReferenceEquals(panel, StatusText) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MostrarEstado(string texto)
    {
        StatusText.Text = texto;
        Mostrar(StatusText);
    }

    public void ShowLogin(string? motivo = null)
    {
        ServerBox.Text = _settings.ServerUrl;
        Mostrar(LoginPanel);

        if (motivo is not null)
        {
            LoginError.Text = motivo;
            LoginError.Visibility = Visibility.Visible;
        }

        Traer();
        EmailBox.Focus();
    }

    private void Traer()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>Cierra la sesión de este equipo y vuelve a la pantalla de entrada.
    ///
    /// Se limpian los dos tokens: dejar el del equipo haría que el siguiente arranque restaurara
    /// sola la sesión que la persona acaba de cerrar.</summary>
    public void SignOut()
    {
        _settings.AccessToken = null;
        _settings.DeviceToken = null;
        _settings.Save();

        _turnos.Clear();
        _checkInId = null;
        PasswordBox.Clear();
        HeaderHint.Text = "";

        ShowLogin();
    }

    // ── Entrada ──────────────────────────────────────────────────────────────────────────────

    // Calificado: con UseWindowsForms activado hay dos KeyEventArgs en scope.
    private void PasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) LoginButton_Click(sender, e);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginError.Visibility = Visibility.Collapsed;
        LoginButton.IsEnabled = false;
        LoginButton.Content = "Entrando…";

        try
        {
            var server = ServerBox.Text.Trim().TrimEnd('/');
            if (server.Length == 0) throw new InvalidOperationException("Falta la dirección del servidor.");
            if (!server.StartsWith("http", StringComparison.OrdinalIgnoreCase)) server = "http://" + server;

            // Corregir solo la dirección, con la sesión todavía válida. Pedir la contraseña ahí
            // sería pedirla por un problema que no tiene nada que ver con la identidad.
            if (PasswordBox.Password.Length == 0 && _settings.HasSession)
            {
                _settings.ServerUrl = server;
                _settings.Save();
                await AbrirCheckInAsync();
                return;
            }

            using var response = await _http.PostAsJsonAsync(
                $"{server}/api/auth/login",
                new { email = EmailBox.Text.Trim(), password = PasswordBox.Password });

            if (!response.IsSuccessStatusCode)
            {
                // El 401 es el caso común y merece su propio texto: «error 401» no le dice a
                // nadie que se equivocó de contraseña.
                throw new InvalidOperationException(
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? "Email o contraseña incorrectos."
                        : $"El servidor respondió {(int)response.StatusCode}. Revisá la dirección.");
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

            _settings.ServerUrl = server;
            _settings.AccessToken = payload.GetProperty("token").GetString();
            _settings.Email = payload.GetProperty("user").GetProperty("email").GetString();
            _settings.UserName = payload.GetProperty("user").GetProperty("name").GetString();
            _settings.Save();

            // Se registra el equipo en el mismo acto: el token que devuelve no vence, así que
            // esta es la última vez que la app pide la contraseña.
            await RegisterDeviceAsync(server);

            PasswordBox.Clear();
            SignedIn?.Invoke();

            await AbrirCheckInAsync();
        }
        catch (Exception ex)
        {
            LoginError.Text = ex is HttpRequestException
                ? $"No se pudo conectar con {ServerBox.Text.Trim()}. ¿Es la dirección correcta y está encendido?"
                : ex.Message;
            LoginError.Visibility = Visibility.Visible;
            Mostrar(LoginPanel);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Entrar";
        }
    }

    private async Task RegisterDeviceAsync(string server)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{server}/api/devices")
            {
                Content = JsonContent.Create(new
                {
                    machineName = Environment.MachineName,
                    osInfo = Environment.OSVersion.VersionString
                })
            };
            request.Headers.Authorization = new("Bearer", _settings.AccessToken);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            _settings.DeviceToken = payload.GetProperty("token").GetString();
            _settings.Save();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            // Sin token de dispositivo la app funciona igual hasta que venza la sesión; recién
            // ahí vuelve a pedir la contraseña. No es motivo para cortar el ingreso.
        }
    }

    /// <summary>Renueva la sesión con el token del equipo. Devuelve false si el equipo ya no está
    /// vinculado —lo revocaron o borraron la cuenta—, que es cuando hay que volver a entrar.</summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.DeviceToken)) return _settings.HasSession;

        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"{_settings.ServerUrl.TrimEnd('/')}/api/auth/device",
                new { deviceToken = _settings.DeviceToken, appVersion = "1.0" });

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _settings.AccessToken = null;
                    _settings.DeviceToken = null;
                    _settings.Save();
                    return false;
                }

                // Un servidor caído no es una sesión inválida: se sigue con lo que había.
                return _settings.HasSession;
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            _settings.AccessToken = payload.GetProperty("token").GetString();
            _settings.Save();
            return true;
        }
        catch (Exception)
        {
            // Cualquier excepción, no tres tipos elegidos: esto corre desde el arranque, que es
            // `async void`, y lo que se escape termina la app antes de mostrar nada.
            return _settings.HasSession;
        }
    }

    // ── El chat ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Trae el check-in de hoy y lo muestra. Es lo único que abre esta ventana.</summary>
    public async Task AbrirCheckInAsync(Guid? id = null)
    {
        if (!_settings.HasSession)
        {
            ShowLogin();
            return;
        }

        Traer();
        MostrarEstado("Buscando tu check-in de hoy…");

        try
        {
            var checkInId = id;

            if (checkInId is null)
            {
                var hoy = await _api.TodayAsync();
                if (hoy is null)
                {
                    MostrarEstado(
                        "No tenés ningún check-in pendiente.\n\n" +
                        "Cuando el agente tenga algo para preguntarte, te avisa por acá.");
                    return;
                }
                checkInId = hoy.Id;
            }

            _checkInId = checkInId;

            // Abrir puede tardar: si es la primera vez, el agente redacta su apertura y con un
            // modelo local en CPU eso son decenas de segundos. Decirlo evita que parezca colgado.
            MostrarEstado("Abriendo el check-in…\nEl agente está mirando tu trabajo.");

            var vista = await _api.OpenAsync(checkInId.Value);
            Pintar(vista);
        }
        catch (SessionExpiredException)
        {
            _settings.AccessToken = null;
            _settings.Save();
            ShowLogin("Tu sesión venció. Entrá de nuevo.");
        }
        catch (HttpRequestException)
        {
            MostrarEstado(
                $"No se pudo conectar con {_settings.ServerUrl}.\n\n" +
                "¿El servidor está encendido? Podés corregir la dirección desde «Cambiar de usuario» " +
                "en el menú de la bandeja.");
        }
        catch (Exception ex)
        {
            MostrarEstado($"No se pudo abrir el check-in.\n\n{ex.Message}");
        }
    }

    /// <summary>Vuelca la conversación en pantalla.</summary>
    private void Pintar(ConversationView? vista)
    {
        if (vista is null)
        {
            MostrarEstado("El servidor no devolvió la conversación.");
            return;
        }

        _turnos.Clear();

        foreach (var turno in vista.Turns)
        {
            var texto = (turno.Text ?? "").Trim();
            if (texto.Length == 0) continue;

            // El primer mensaje del transcript es la instrucción con la que arrancamos al agente,
            // no algo que la persona haya escrito. Mostrarla la confundiría.
            var esPersona = turno.Role.Equals("user", StringComparison.OrdinalIgnoreCase);
            if (esPersona && _turnos.Count == 0) continue;

            _turnos.Add(new ChatBubble(esPersona ? BubbleKind.Persona : BubbleKind.Agente, texto));
        }

        foreach (var accion in vista.AppliedActions)
        {
            _turnos.Add(new ChatBubble(BubbleKind.Accion, $"✓ {accion}"));
        }

        foreach (var pendiente in vista.PendingActions)
        {
            _turnos.Add(new ChatBubble(BubbleKind.Accion, $"⏳ {pendiente} — espera aprobación"));
        }

        if (vista.IsClosed)
        {
            if (!string.IsNullOrWhiteSpace(vista.Summary))
            {
                _turnos.Add(new ChatBubble(BubbleKind.Accion, vista.Summary));
            }

            HeaderHint.Text = "check-in cerrado";
            ReplyBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            NoChangesButton.IsEnabled = false;
        }
        else
        {
            HeaderHint.Text = "check-in de hoy";
            ReplyBox.IsEnabled = true;
            SendButton.IsEnabled = true;
            NoChangesButton.IsEnabled = true;
        }

        Mostrar(ChatPanel);
        ChatScroll.ScrollToEnd();
        if (!vista.IsClosed) ReplyBox.Focus();
    }

    private void ReplyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Enter manda; Shift+Enter hace salto de línea.
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;

        e.Handled = true;
        _ = EnviarAsync();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => _ = EnviarAsync();

    private async Task EnviarAsync()
    {
        var texto = ReplyBox.Text.Trim();
        if (texto.Length == 0 || _checkInId is null || _ocupado) return;

        _ocupado = true;
        ChatError.Visibility = Visibility.Collapsed;
        ReplyBox.Clear();

        // El mensaje se pinta antes de que conteste el servidor. Con un modelo local la respuesta
        // tarda; sin esto la persona escribe, ve desaparecer su texto y no pasa nada más.
        _turnos.Add(new ChatBubble(BubbleKind.Persona, texto));
        _turnos.Add(new ChatBubble(BubbleKind.Agente, "…"));
        ChatScroll.ScrollToEnd();

        SendButton.IsEnabled = false;
        SendButton.Content = "…";

        try
        {
            var vista = await _api.ReplyAsync(_checkInId.Value, texto);
            Pintar(vista);
        }
        catch (SessionExpiredException)
        {
            ShowLogin("Tu sesión venció. Entrá de nuevo.");
        }
        catch (Exception ex)
        {
            // Se saca el «…» y se devuelve el texto, para que no haya que reescribirlo.
            if (_turnos.Count > 0) _turnos.RemoveAt(_turnos.Count - 1);
            ReplyBox.Text = texto;

            ChatError.Text = ex is HttpRequestException ? "Se cortó la conexión." : ex.Message;
            ChatError.Visibility = Visibility.Visible;
        }
        finally
        {
            _ocupado = false;
            SendButton.IsEnabled = true;
            SendButton.Content = "Enviar";
        }
    }

    private async void NoChangesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_checkInId is null || _ocupado) return;

        _ocupado = true;
        NoChangesButton.IsEnabled = false;

        try
        {
            Pintar(await _api.NoChangesAsync(_checkInId.Value));
        }
        catch (Exception ex)
        {
            ChatError.Text = ex.Message;
            ChatError.Visibility = Visibility.Visible;
            NoChangesButton.IsEnabled = true;
        }
        finally
        {
            _ocupado = false;
        }
    }

    private void OpenWebButton_Click(object sender, RoutedEventArgs e) => OpenInBrowser("/");

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        HideRequested?.Invoke();
        base.OnClosing(e);
    }
}
