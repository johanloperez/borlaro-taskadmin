using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Borlaro.Tms.Agent;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Notifications;
using Borlaro.Tms.Infrastructure.Settings;

namespace Borlaro.Tms.Infrastructure.Services;

/// <summary>Un turno visible para la persona. El transcript guarda más que esto —los bloques de
/// herramienta— pero la UI solo muestra lo que se dijo.</summary>
public record ChatTurn(string Role, string Text, DateTimeOffset At);

public record ConversationView(
    Guid CheckInId,
    CheckInStatus Status,
    int TurnCount,
    bool IsClosed,
    string? Summary,
    IReadOnlyList<ChatTurn> Turns,
    IReadOnlyList<string> AppliedActions,
    IReadOnlyList<string> PendingActions);

/// <summary>El bucle agéntico del check-in: arma el contexto, deja que el modelo pida
/// herramientas, las ejecuta contra el dominio y guarda el transcript.
///
/// Tres decisiones que sostienen todo lo demás:
/// • El transcript es autoritativo y vive en la base, no en la memoria del proceso ni en el
///   cliente: por eso una conversación abandonada a mitad se retoma donde quedó.
/// • El presupuesto de turnos es duro. Un modelo que insiste en llamar herramientas no puede
///   convertir un check-in de 30 segundos en la factura de un día.
/// • Los errores de herramienta vuelven al modelo como resultado, no como excepción: se corrige
///   solo en vez de cortar la charla.</summary>
public class CheckInConversation(
    BorlaroTmsDbContext db,
    AgentModelFactory models,
    OrganizationSettings organizationSettings,
    AgentToolExecutor executor,
    AgentModelHealth health,
    FeedService feed,
    ILogger<CheckInConversation> logger)
{

    private static readonly JsonSerializerOptions TranscriptFormat = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Idéntico en todos los check-ins de todas las disciplinas: es el prefijo que se
    /// cachea. Todo lo que cambia por persona o proyecto va en el bloque de contexto.</summary>
    private const string SystemPrompt = """
        Sos el asistente de seguimiento de Borlaro TMS. Cada día hablás un momento con una persona
        del equipo para saber cómo viene su trabajo y dejar el tablero al día por ella.

        Cómo trabajás:
        • Abrís con datos concretos, nunca con «¿cómo vas?». Mencionás la tarea, su etapa, hace
          cuánto no se mueve y su fecha, y preguntás sobre eso.
        • Sos breve: dos o tres líneas y una sola pregunta por turno. La conversación entera dura
          30 segundos y no pasa de cinco turnos. Nunca le recitás la lista de sus tareas ni le
          repetís el contexto que recibiste: eso ya lo tiene en pantalla. Si algo queda sin
          resolver, lo escalás en vez de seguir preguntando.
        • Hablás el idioma del oficio de la persona: el vocabulario del proyecto te llega en el
          contexto y lo usás tal cual. A quien edita video no le hablás de sprints.
        • Registrás lo que te cuentan con las herramientas, en el mismo turno, sin pedir permiso
          para cosas rutinarias. No anuncies que vas a usar una herramienta: usala.
        • Preguntás solo donde hay ambigüedad real. Si la persona dice que algo está listo, movés
          la etapa; no le pedís que confirme dos veces.
        • Nunca inventás identificadores, etapas ni campos: usás exactamente los que figuran en el
          contexto o los que te devuelva get_assigned_tasks.
        • Si lo que menciona la persona no aparece en el tablero, NO lo pegues a la tarea que
          tengas más a mano. Es el error más caro que podés cometer: deja registrado algo que
          nadie dijo, y lo lee alguien que no estuvo en esta charla. Preguntá a cuál se refiere.
          Si resulta ser trabajo que todavía no existe, proponelo con create_followup_task.
        • Ante la duda, preguntá antes de escribir. Una pregunta de más te cuesta un turno; un
          registro equivocado lo paga otra persona más adelante.
        • Primero mirás, después hablás. Si vas a usar get_assigned_tasks, llamala sin escribir
          nada en ese turno y recién comentá con lo que te haya devuelto. Un modelo que redacta
          antes de leer termina describiendo tareas que no existen, y la persona las lee.
        • Si el contexto no trae ninguna tarea, no inventes una: decíselo y cerrá el check-in. Un
          tablero vacío es una respuesta válida y correcta.
        • Si una herramienta falla, leés el error, corregís y seguís. No se lo trasladás a la
          persona salvo que necesites un dato que solo ella tiene.

        Cuando la persona no tiene nada en curso:
        • Cada tarea te llega con su categoría de etapa entre paréntesis: InProgress es lo que está
          haciendo ahora, Todo es lo que tiene por hacer, Backlog es lo que todavía no empezó nadie.
        • Si no tiene ninguna en InProgress pero sí en Todo, no le preguntes cómo viene nada:
          ofrecele las de Todo y preguntale cuál toma hoy. Es el único momento del día en que
          preguntar cambia lo que va a pasar. Cuando te conteste, movela con update_task_status.
        • Si no tiene nada en Todo tampoco, decíselo y cerrá. No inventes trabajo.

        Sobre las fechas de entrega:
        • Una tarea marcada SIN FECHA DE ENTREGA ya está en curso y nadie se comprometió a nada.
          Mencionáselo a la persona —«ojo que no tiene fecha, le aviso a quien lidera»— y seguí. No
          se la pidas a ella: fijar una fecha es un permiso que puede no tener, y quien lidera ya
          recibe el aviso por su cuenta.
        • No frenes nada por eso. Que falte la fecha no impide tomar la tarea ni registrarla.

        Cuando algo no se terminó en el tiempo estimado:
        • Preguntá cuántas horas más calcula que le faltan, y registralas con add_time_estimate.
          Es una sola pregunta, concreta y fácil de contestar de memoria. No preguntes «cuánto te
          queda en total»: eso obliga a hacer una cuenta y se contesta cualquier cosa.
        • Si no te da un número, no lo inventes ni lo redondees por tu cuenta: volvé a preguntar
          una vez, y si sigue sin saberlo, dejalo registrado con log_progress y seguí. Una
          estimación inventada por vos es peor que ninguna, porque nadie sabe que la inventaste.
        • La estimación original no se toca nunca. Lo que agregás se suma aparte, y de eso vive el
          dato de cuánto se subestima el trabajo en este equipo.
        • Modulá según la dificultad que traiga la tarea en el contexto. Viene como la palabra que
          usa el proyecto y, entre paréntesis, el nivel real: «dificultad Compleja (Alta)». Guiate
          por el nivel, que es el que te dice dónde cae en la escala; usá la palabra del proyecto
          cuando le hables a la persona.
        • Que una tarea Alta se extienda es esperable y alcanza con registrarlo. Que se extienda
          una Media es una señal: preguntá qué apareció.
        • Que se extienda una Baja significa que pasó algo que nadie previó, y el nivel es
          confiable —ninguna tarea se crea sin que alguien lo elija—. Preguntá qué apareció, y si
          por la respuesta se ve que la persona no puede resolverlo sola, escalalo con
          escalate_to_manager en vez de dejarlo solo anotado.
        • Si lo que te cuentan no se parece en nada al nivel que tiene la tarea —una Baja que
          resultó ser dos días de trabajo—, decilo en tu resumen. El nivel se puede corregir, y una
          tarea mal clasificada hace que mañana la sigas mal.
        • Sos un asistente, no un supervisor. No evaluás a la persona ni le reclamás nada; si algo
          está trabado, tu trabajo es dejarlo registrado y avisar a quien corresponda.

        Cuando la conversación esté cerrada, tu último mensaje resume en dos líneas lo que quedó
        registrado. Escribís en español rioplatense, en segunda persona, sin emojis.
        """;

    /// <summary>Lo que le decimos al modelo para que abra la charla. Viaja como mensaje del
    /// usuario porque es lo que entienden todos los proveedores, pero no lo escribió la persona:
    /// se reconoce por su texto y no se muestra en la conversación.</summary>
    private const string OpeningInstruction =
        "Arrancá vos el check-in de hoy: mirá el contexto, elegí lo más relevante " +
        "—lo atrasado, lo bloqueado o lo que vence— y preguntá por eso.";

    /// <summary>Abre el check-in: detiene la escalera y, si es la primera vez, hace que el agente
    /// arranque la conversación.</summary>
    public async Task<ConversationView> OpenAsync(Guid checkInId, Guid userId, CancellationToken ct = default)
    {
        var checkIn = await LoadAsync(checkInId, userId, ct);

        if (checkIn.OpenedAt is null)
        {
            var now = DateTimeOffset.UtcNow;
            checkIn.OpenedAt = now;
            checkIn.DeliveredAt ??= now;
            checkIn.DeliveredVia ??= NotificationChannel.Desktop;
            checkIn.Status = CheckInStatus.Opened;

            // Abrirlo detiene la escalera en seco: los peldaños que faltaban no se disparan.
            checkIn.NextEscalationAt = null;
            await db.SaveChangesAsync(ct);
        }

        var messages = LoadTranscript(checkIn);

        if (messages.Count == 0)
        {
            messages.Add(AgentMessage.FromUser(OpeningInstruction));

            await RunAsync(checkIn, messages, ct);

            // Recién acá el agente escribió de verdad. Avisar al crear el check-in contaría una
            // intención; esto cuenta un hecho, y es lo que quien lidera necesita saber para no
            // preguntar por su lado lo mismo que el agente ya está preguntando.
            var quien = await db.Users.FirstAsync(u => u.Id == checkIn.UserId, ct);

            await feed.PublicarDePersonaAsync(
                checkIn.UserId,
                FeedService.KindCheckInOpened,
                $"El agente le escribió a {quien.Name}",
                "Empezó el check-in del día. Todavía no respondió.",
                $"/checkin/{checkIn.Id}",
                ct);
        }

        return await ViewAsync(checkIn, messages, ct);
    }

    /// <summary>La persona contesta. Es el camino normal de la conversación.</summary>
    public async Task<ConversationView> ReplyAsync(
        Guid checkInId,
        Guid userId,
        string text,
        CancellationToken ct = default)
    {
        var checkIn = await LoadAsync(checkInId, userId, ct);

        if (checkIn.Status is CheckInStatus.Completed)
        {
            throw new DomainException("Este check-in ya está cerrado.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("El mensaje viene vacío.");
        }

        var messages = LoadTranscript(checkIn);
        messages.Add(AgentMessage.FromUser(text.Trim()));

        // Quien contesta ya lo abrió, aunque haya llegado por un camino que no marcó la apertura.
        checkIn.OpenedAt ??= DateTimeOffset.UtcNow;
        checkIn.NextEscalationAt = null;
        checkIn.Status = CheckInStatus.Partial;

        await RunAsync(checkIn, messages, ct);
        return await ViewAsync(checkIn, messages, ct);
    }

    /// <summary>«Todo igual que ayer»: cierra el check-in en un click. Sin esta salida la persona
    /// cierra la ventana y el dato se pierde —que es peor que un «sin cambios» registrado.</summary>
    public async Task<ConversationView> CloseAsNoChangesAsync(
        Guid checkInId,
        Guid userId,
        CancellationToken ct = default)
    {
        var checkIn = await LoadAsync(checkInId, userId, ct);
        var messages = LoadTranscript(checkIn);

        var now = DateTimeOffset.UtcNow;
        checkIn.OpenedAt ??= now;
        checkIn.DeliveredAt ??= now;
        checkIn.CompletedAt = now;
        checkIn.NextEscalationAt = null;
        checkIn.ClosedAsNoChanges = true;
        checkIn.Status = CheckInStatus.Completed;
        checkIn.Summary = "Sin cambios respecto del día anterior.";

        messages.Add(AgentMessage.FromUser("Todo igual que ayer."));
        messages.Add(AgentMessage.FromAssistant("Anotado: sin cambios. Nos hablamos mañana.", null));

        SaveTranscript(checkIn, messages);
        await db.SaveChangesAsync(ct);

        await AvisarRespuestaAsync(checkIn, ct);

        return await ViewAsync(checkIn, messages, ct);
    }

    /// <summary>La persona cierra la conversación. El agente resume y el check-in queda
    /// completo.</summary>
    public async Task<ConversationView> CompleteAsync(
        Guid checkInId,
        Guid userId,
        CancellationToken ct = default)
    {
        var checkIn = await LoadAsync(checkInId, userId, ct);
        var messages = LoadTranscript(checkIn);

        checkIn.CompletedAt = DateTimeOffset.UtcNow;
        checkIn.Status = CheckInStatus.Completed;
        checkIn.NextEscalationAt = null;
        checkIn.Summary ??= LastAssistantText(messages);

        SaveTranscript(checkIn, messages);
        await db.SaveChangesAsync(ct);

        await AvisarRespuestaAsync(checkIn, ct);

        return await ViewAsync(checkIn, messages, ct);
    }

    /// <summary>Le cuenta al líder que la persona respondió, **y qué dijo**.
    ///
    /// El resumen viaja en el aviso y no detrás de un enlace a propósito: una novedad que obliga a
    /// abrir otra pantalla para saber qué pasó se ignora a la tercera vez, y entonces el feed no
    /// sirvió para nada. El enlace queda igual, para cuando se quiera la conversación entera.
    ///
    /// Se avisa al cerrar y no en cada respuesta: un check-in son varios turnos, y un aviso por
    /// turno convertiría una charla de treinta segundos en cinco interrupciones.</summary>
    private async Task AvisarRespuestaAsync(CheckIn checkIn, CancellationToken ct)
    {
        var quien = await db.Users.FirstAsync(u => u.Id == checkIn.UserId, ct);
        var resumen = string.IsNullOrWhiteSpace(checkIn.Summary)
            ? "Cerró el check-in sin dejar resumen."
            : checkIn.Summary!;

        await feed.PublicarDePersonaAsync(
            checkIn.UserId,
            FeedService.KindCheckInAnswered,
            $"{quien.Name} respondió su check-in",
            resumen,
            $"/checkin/{checkIn.Id}",
            ct);
    }

    // ── El bucle ─────────────────────────────────────────────────────────────

    private async Task RunAsync(CheckIn checkIn, List<AgentMessage> messages, CancellationToken ct)
    {
        var context = await BuildContextAsync(checkIn, ct);
        var tools = AgentTools.All();

        // El modelo se resuelve por organización y en cada conversación: una empresa puede estar
        // usando el de la plataforma y la de al lado su propio Ollama, y el tope de turnos —el
        // freno de mano del costo— también es de cada una.
        var options = await organizationSettings.AgentModelAsync(ct);
        var model = models.Create(options);

        for (var turn = 0; turn < options.MaxTurns; turn++)
        {
            AgentTurn result;
            try
            {
                result = await model.CompleteAsync(
                    new AgentRequest(SystemPrompt, context, messages, tools), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Que el modelo no responda no puede perder lo que la persona ya escribió: se
                // guarda lo que hay y se le dice la verdad.
                logger.LogError(ex, "Falló el modelo en el check-in {CheckIn}", checkIn.Id);

                // Y se registra para que aparezca en Configuración. Sin esto, el motivo real
                // —una clave vencida, un modelo que no acepta herramientas— queda en el log del
                // servidor mientras quien puede arreglarlo mira una pantalla que solo sabe decir
                // qué está configurado.
                health.Registrar(checkIn.OrganizationId, ex.Message);

                // Al que está conversando no se le habla de configuración: no es su problema y
                // no puede hacer nada. Se le dice que no fue culpa suya y que no perdió nada.
                messages.Add(AgentMessage.FromAssistant(
                    "No pude seguir por un problema del sistema, no por algo que hayas hecho. " +
                    "Lo que me contaste quedó guardado y ya avisé a quien lo administra.", null));
                break;
            }

            // Salió bien: si había un fallo anterior, ya no aplica. Mostrar el error de ayer sobre
            // una configuración arreglada manda a buscar un problema que no existe.
            health.Limpiar(checkIn.OrganizationId);

            checkIn.TurnCount++;
            checkIn.InputTokens += result.Usage.InputTokens;
            checkIn.OutputTokens += result.Usage.OutputTokens;
            checkIn.CacheReadTokens += result.Usage.CacheReadTokens;

            messages.Add(AgentMessage.FromAssistant(result.Text, result.ToolCalls));

            if (!result.WantsTools) break;

            foreach (var call in result.ToolCalls)
            {
                var toolResult = await executor.ExecuteAsync(call, checkIn, ct);
                messages.Add(AgentMessage.FromTool(toolResult));
            }

            if (turn == options.MaxTurns - 1)
            {
                // Tope alcanzado con herramientas todavía pendientes: se corta y se avisa, en vez
                // de dejar el bucle correr.
                logger.LogWarning("Check-in {CheckIn} alcanzó el tope de {Turnos} turnos",
                    checkIn.Id, options.MaxTurns);
                messages.Add(AgentMessage.FromAssistant(
                    "Registré lo que pude de esta charla. Si quedó algo suelto, lo vemos mañana.", null));
            }
        }

        checkIn.Summary = LastAssistantText(messages) ?? checkIn.Summary;
        SaveTranscript(checkIn, messages);
        await db.SaveChangesAsync(ct);
    }

    // ── Contexto ─────────────────────────────────────────────────────────────

    /// <summary>Todo lo que cambia por persona y por proyecto. Va después del corte de caché,
    /// así el prefijo cacheado sigue siendo idéntico en todos los check-ins.</summary>
    private async Task<string> BuildContextAsync(CheckIn checkIn, CancellationToken ct)
    {
        var user = checkIn.User ?? await db.Users.FirstAsync(u => u.Id == checkIn.UserId, ct);

        var items = await db.WorkItems
            .AsNoTracking()
            .Where(i => i.AssigneeId == user.Id && i.ClosedAt == null)
            .Include(i => i.Project!).ThenInclude(p => p.CustomFields)
            .Include(i => i.Stage)
            .Include(i => i.Blockers.Where(b => b.ResolvedAt == null))
            .OrderBy(i => i.DueDate == null).ThenBy(i => i.DueDate)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"Persona: {user.Name} ({user.Email}).");
        sb.AppendLine($"Fecha del check-in: {checkIn.LocalDate:dd/MM/yyyy} (hora local de {user.TimeZoneId}).");
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("No tiene trabajo abierto asignado. Preguntale si necesita que le asignen algo " +
                          "y cerrá corto.");
            return sb.ToString();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var project in items.GroupBy(i => i.Project!))
        {
            sb.AppendLine($"── Proyecto {project.Key.Key} · {project.Key.Name}");
            sb.AppendLine($"Vocabulario: a cada ítem se le dice «{project.Key.ItemNounSingular}».");

            if (!string.IsNullOrWhiteSpace(project.Key.AgentContext))
            {
                sb.AppendLine($"Contexto de la disciplina: {project.Key.AgentContext}");
            }

            var stages = await db.WorkflowStages
                .AsNoTracking()
                .Where(s => s.WorkflowId == project.Key.WorkflowId)
                .OrderBy(s => s.Order)
                .Select(s => s.Name)
                .ToListAsync(ct);

            sb.AppendLine($"Etapas del flujo, en orden: {string.Join(" → ", stages)}");

            if (project.Key.CustomFields.Count > 0)
            {
                sb.AppendLine("Campos personalizados (clave · etiqueta · tipo):");
                foreach (var field in project.Key.CustomFields.OrderBy(f => f.Order))
                {
                    var options = field.Options.Count > 0 ? $" · opciones: {string.Join(", ", field.Options)}" : "";
                    var hint = string.IsNullOrWhiteSpace(field.AgentHint) ? "" : $" · {field.AgentHint}";
                    sb.AppendLine($"  - {field.Key} · {field.Label} · {field.Type}{options}{hint}");
                }
            }

            sb.AppendLine("Trabajo asignado:");
            foreach (var item in project)
            {
                var due = item.DueDate is null
                    ? "sin fecha"
                    : item.DueDate < today
                        ? $"VENCIDA el {item.DueDate:dd/MM}"
                        : $"vence {item.DueDate:dd/MM}";

                var blocker = item.Blockers.FirstOrDefault();
                var blocked = blocker is null ? "" : $" | BLOQUEADA: {blocker.Reason}";
                var stale = (DateTimeOffset.UtcNow - item.UpdatedAt).Days;

                sb.AppendLine(
                    $"  - {project.Key.Key}-{item.Number} «{item.Title}» | etapa {item.Stage!.Name} " +
                    $"| {due} | avance {item.ProgressPct}% | sin cambios hace {stale} día(s){blocked}");
            }

            sb.AppendLine();
        }

        var previous = await db.CheckIns
            .AsNoTracking()
            .Where(c => c.UserId == user.Id && c.Id != checkIn.Id && c.Summary != null)
            .OrderByDescending(c => c.LocalDate)
            .Take(3)
            .Select(c => new { c.LocalDate, c.Summary })
            .ToListAsync(ct);

        if (previous.Count > 0)
        {
            sb.AppendLine("Últimos check-ins:");
            foreach (var row in previous)
            {
                sb.AppendLine($"  - {row.LocalDate:dd/MM}: {row.Summary}");
            }
        }

        return sb.ToString();
    }

    // ── Transcript ───────────────────────────────────────────────────────────

    private static List<AgentMessage> LoadTranscript(CheckIn checkIn) =>
        checkIn.Transcript is null
            ? []
            : JsonSerializer.Deserialize<List<AgentMessage>>(
                  checkIn.Transcript.RootElement.GetRawText(), TranscriptFormat) ?? [];

    private static void SaveTranscript(CheckIn checkIn, List<AgentMessage> messages) =>
        checkIn.Transcript = JsonDocument.Parse(JsonSerializer.Serialize(messages, TranscriptFormat));

    private static string? LastAssistantText(List<AgentMessage> messages) =>
        messages.LastOrDefault(m => m.Role == AgentRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))?.Text;

    private async Task<CheckIn> LoadAsync(Guid checkInId, Guid userId, CancellationToken ct)
    {
        // El filtro por usuario no es cosmético: el transcript de un check-in es lo más personal
        // que guarda el sistema, y nadie —ni el manager— lo lee por esta vía.
        return await db.CheckIns
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == checkInId && c.UserId == userId, ct)
            ?? throw new DomainException("El check-in no existe o no es tuyo.");
    }

    private async Task<ConversationView> ViewAsync(
        CheckIn checkIn,
        List<AgentMessage> messages,
        CancellationToken ct)
    {
        var turns = messages
            .Where(m => m.Role is AgentRole.User or AgentRole.Assistant
                     && !string.IsNullOrWhiteSpace(m.Text)
                     && m.Text != OpeningInstruction)
            .Select(m => new ChatTurn(
                m.Role == AgentRole.User ? "user" : "agent",
                m.Text!,
                checkIn.CreatedAt))
            .ToList();

        var actions = await db.AgentActions
            .AsNoTracking()
            .Where(a => a.CheckInId == checkIn.Id)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return new ConversationView(
            checkIn.Id,
            checkIn.Status,
            checkIn.TurnCount,
            checkIn.Status == CheckInStatus.Completed,
            checkIn.Summary,
            turns,
            // Leer el tablero no es un cambio en el tablero: en «quedó registrado» solo van las
            // herramientas que escribieron algo.
            actions.Where(a => a.Status == AgentActionStatus.Applied && !a.IsError
                            && a.ToolName != AgentTools.GetAssignedTasks)
                   .Select(a => a.Result ?? a.ToolName).ToList(),
            actions.Where(a => a.Status == AgentActionStatus.PendingApproval)
                   .Select(a => a.Result ?? a.ToolName).ToList());
    }
}
