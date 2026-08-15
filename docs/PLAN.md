# TaskAdmin — Gestión de trabajo con agente de IA de seguimiento

## Contexto

Necesitas un gestor de tareas para **equipos de trabajo de cualquier disciplina** —desarrollo, diseño, edición de video, contenido, marketing, operaciones— donde un analista o lead crea la tarea (como un issue de GitLab), la asigna a alguien, y la tarea recorre una serie de etapas.

El diferenciador no es el tracker —eso ya existe— sino un **agente de IA que cada día conversa con cada colaborador**, le muestra lo pendiente, le pregunta el estado, y en base a la respuesta **mueve, reordena, extiende o escala** las tareas y avisa al manager. El dashboard queda actualizado sin que nadie tenga que actualizarlo a mano.

Restricciones que fijaste: servidor mediano/liviano (no cluster), frontend React con diseño de primer nivel, backend en C# (.NET), MVP delgado pero con IA desde el día 1, y el repositorio configurable (fuente propia o GitHub, con migración amigable).

**La generalidad es un requisito de diseño, no un "después".** Se resuelve con tres mecanismos que atraviesan todo el plan: **plantillas de proyecto** (workflow + campos + vocabulario por disciplina), **campos personalizados** por proyecto, y **conectores de evidencia** intercambiables —donde el repositorio Git es *uno* de ellos, junto a entregables subidos y enlaces externos (Figma, Drive, Frame.io).

Directorio actual: vacío. Proyecto desde cero.

---

## 1. Qué hay en el mercado

### Trackers de ingeniería (profundidad en un oficio)

| Producto | Qué hace bien | Punto débil |
|---|---|---|
| **Jira** | Estándar corporativo: workflows configurables, campos custom, reportes profundos, Rovo AI (triage, issues desde Slack, redacción de status) | Sobredimensionado bajo ~20 personas; pesado de operar y aprender |
| **Linear** | Referencia de UX: instantáneo, atajos de teclado, cero fricción. Linear Agent y updates automáticos desde issues y documentos | SaaS cerrado; solo sirve a equipos de software |
| **GitHub / GitLab Issues** | Pegados al código: labels, milestones, vínculo issue↔PR/MR; GitLab suma CI/CD | Sin etapas ricas, sin estimaciones, inservible fuera de desarrollo |

### Gestión de trabajo general (amplitud entre oficios)

**Asana**, **monday.com**, **ClickUp**, **Notion** cubren cualquier equipo: monday.com gana en coordinar trabajo entre personas y departamentos; Notion gana en escribir y organizar información; Asana ofrece estructura predecible con setup mínimo; ClickUp es el que mejor modela workflows complejos y personalizados. Sus plantillas se orientan a operaciones (planes de proyecto, CRM, campañas) y productividad (calendarios de contenido, notas).

Para equipos creativos el patrón consolidado es: formulario de intake con el brief, una plantilla de producción con dependencias y deadlines, y **proofing** (Frame.io, Ziflow) para las rondas de revisión y aprobación de video y diseño.

### Bots de standup asíncrono (lo más cercano a tu idea)

**Geekbot**, **DailyBot**, **Standuply**, **Steady**: preguntan en Slack/Teams "¿qué hiciste ayer / qué harás hoy / bloqueos?", juntan las respuestas y publican un resumen.

**Su límite —y tu oportunidad—:** son *encuestas*, no agentes. Recolectan texto libre y lo publican; **no leen el backlog de la persona, no saben qué tarea está atrasada, y no escriben nada de vuelta en el tracker**. El manager sigue leyendo párrafos y actualizando el board a mano.

Del otro lado, la IA de Jira/Linear/Asana *resume* lo que ya pasó —los resúmenes automáticos reducen el tiempo de reuniones de status entre 20% y 35%— pero **no persigue a nadie**: si la persona no toca el ticket, no hay nada que resumir.

### El hueco

> Nadie une las dos mitades: **un agente que conoce el trabajo asignado de cada persona, la entrevista todos los días, y traduce la respuesta en mutaciones concretas del tablero.**

Y hay un segundo hueco que refuerza el primero: los standup bots viven en Slack, que muchos equipos chicos no pagan. Tu agente de escritorio con notificación nativa de Windows y fallback por email llega a la persona sin depender de una suscripción por usuario — y llega igual a un editor de video que a un backend, que es precisamente lo que ninguno de los dos bandos hace hoy.

---

## 2. Funciones que no pueden faltar (paridad de mercado)

Sin esto no compite con nada. ✅ = MVP · ⏳ = fase 2

**Tareas** — ✅ título, descripción markdown, tipo, prioridad, responsable, solicitante, estimación (horas o puntos), fecha objetivo, etiquetas, ID legible (`DIS-142`), adjuntos, comentarios con @menciones.

**Campos personalizados** — ✅ por proyecto: texto, número, lista de opciones, fecha, casilla, URL, archivo, persona. Es el mecanismo que hace que el mismo sistema sirva a un equipo de video (duración, códec, versión de corte) y a uno de backend (entorno, versión de release) sin código nuevo.

**Flujo de estados** — ✅ etapas configurables por proyecto, con transiciones válidas (no saltar de Backlog a Hecho) y `Bloqueado` obligando a declarar motivo (esto alimenta a la IA).

**Revisión y aprobación** — ✅ rondas de revisión con versiones del entregable, aprobar / pedir cambios con comentarios, y registro de cuántas vueltas llevó. Es el equivalente del *pull request* para diseño y edición, y es el punto donde los trackers de ingeniería fallan a los equipos creativos.

**Organización** — ✅ proyectos, ✅ backlog priorizado, ⏳ épicas/campañas, ⏳ ciclos (sprints, semanas de producción), ⏳ dependencias entre tareas.

**Vistas** — ✅ tablero Kanban con drag & drop, ✅ lista filtrable, ✅ "mis tareas", ⏳ calendario/timeline, ⏳ carga por persona.

**Colaboración** — ✅ comentarios, ✅ historial completo (quién, qué, cuándo), ✅ notificaciones.

**Reportes** — ✅ dashboard del manager (en curso / atrasadas / bloqueadas / por persona), ⏳ tiempo por etapa (cycle time), ⏳ rondas de revisión promedio, ⏳ burndown.

**Permisos** — ✅ roles Admin / Manager–Lead / Colaborador / Cliente-revisor (solo ve y aprueba lo suyo).

**Evidencia externa** — ✅ enlaces a entregables (Figma, Drive, Frame.io) y archivos subidos con versiones; ✅ repositorio Git por proyecto con commits y PRs enlazados a `DEV-142`.

**Integraciones** — ✅ webhooks salientes, ✅ API REST documentada, ✅ formulario de intake público por proyecto (el brief entra como tarea).

---

## 3. Cómo un solo sistema sirve a cualquier equipo

Tres mecanismos, ninguno opcional:

### a) Plantillas de proyecto

Al crear un proyecto se elige una plantilla que preconfigura **workflow + campos personalizados + tipos de tarea + vocabulario + contexto del agente**. Son datos en la base, no código: se editan y se crean nuevas desde la UI.

| Plantilla | Etapas | Campos propios | Tipos |
|---|---|---|---|
| **Desarrollo** | Backlog → Por hacer → En curso → Bloqueado → En revisión → QA → Hecho | Entorno, versión, rama | Bug, feature, tarea, spike |
| **Diseño** | Brief → En cola → Diseñando → Revisión interna → Revisión cliente → Ajustes → Aprobado | Formato, medidas, marca, entregable | Pieza, sistema, revisión |
| **Edición / video** | Brief → Guion → Rodaje → Montaje → Corrección color → Revisión → Ajustes → Publicado | Duración, plataforma, versión de corte, música | Video, corto, reel |
| **Contenido** | Idea → Investigación → Redacción → Edición → Aprobación → Programado → Publicado | Palabra clave, canal, fecha de publicación | Artículo, newsletter, post |
| **Genérico** | Por hacer → En curso → Bloqueado → Revisión → Hecho | — | Tarea |

El prefijo del ID sale de la clave del proyecto (`DEV-142`, `DIS-88`, `VID-31`), así que el lenguaje del tablero es el del equipo desde el primer día.

### b) Campos personalizados

Definidos por proyecto, con tipo y validación, visibles en el formulario, filtrables en la lista y **legibles por el agente**. Un editor de video que responde *"la 31 está en color, me falta el master 4K"* actualiza etapa y campo `versión de corte` en la misma frase.

### c) Conectores de evidencia

Interfaz `IEvidenceConnector` — el agente pregunta "¿qué prueba hay de que esto avanzó?" y cada disciplina responde distinto:

| Conector | Evidencia | Fase |
|---|---|---|
| **Entregables** | Archivos subidos con versiones (v1, v2, v3) y rondas de revisión | MVP |
| **Enlaces externos** | URL a Figma / Drive / Frame.io / Notion, con etiqueta y fecha de última revisión | MVP |
| **Git** | Commits, ramas y PRs que mencionan `DEV-142`, vía webhooks de solo lectura | Fase 4 |

**El repositorio deja de ser el eje y pasa a ser un conector más.** Esa fue la corrección de rumbo importante: si el diseño hubiera quedado atado a Git, el sistema seguiría siendo un tracker de programadores con etiquetas cambiadas.

---

## 4. El diferenciador: el agente de seguimiento

### Cómo funciona un check-in

1. **Disparo.** Cada día a la hora configurada por persona (zona horaria y días laborables propios), un job encola un check-in.
2. **Entrega.** El servidor empuja la conversación al **agente de escritorio** (icono en bandeja del sistema, notificación toast de Windows). Si el agente no está conectado hace más de N minutos → **email** con enlace al chat web.
3. **Apertura contextual.** El agente **no** pregunta en frío, y habla el idioma del proyecto:
   - a un backend: *"TSK-142 (auth OAuth) lleva 4 días en En curso, estimada en 8h, vence mañana. ¿Cómo venís?"*
   - a una editora: *"VID-31 sigue en Montaje desde el martes y el brief pide publicar el viernes. ¿Llegás o movemos la fecha?"*
   - a un diseñador: *"DIS-88 está en Revisión cliente hace 6 días sin respuesta. ¿La escalo o seguís esperando?"*
4. **Conversación corta.** 2–5 turnos. Repregunta solo donde hay ambigüedad.
5. **Acciones.** Ejecuta herramientas contra la API: cambiar etapa, registrar avance, actualizar campos personalizados, mover fecha, marcar bloqueo, reordenar prioridades, escalar.
6. **Cierre.** Resumen de lo registrado, y entrada en el feed del manager si hubo algo relevante.

### Herramientas que la IA puede ejecutar

| Herramienta | Qué hace | Aprobación |
|---|---|---|
| `get_assigned_tasks` | Lee el trabajo asignado a la persona | — |
| `update_task_status` | Cambia etapa (respetando transiciones válidas) | automática |
| `log_progress` | Registra % de avance + nota del día | automática |
| `set_custom_field` | Actualiza un campo personalizado del proyecto | automática |
| `flag_blocker` | Marca bloqueo con causa y de quién depende | automática |
| `reprioritize_day` | Reordena el foco del día | automática |
| `request_date_change` | Propone mover la fecha objetivo | **requiere manager** |
| `escalate_to_manager` | Levanta alerta (riesgo, revisión estancada, bloqueo persistente) | automática |
| `create_followup_task` | Crea tarea que surgió de la charla | **requiere lead** |

**Regla dura:** toda acción de la IA queda en el historial marcada con origen `ai_agent` y el `check_in_id` que la generó, y es reversible por el manager con un click. Lo que toca fechas comprometidas o crea trabajo nuevo se propone, no se aplica solo.

### El otro lado: el asistente del manager

Chat contra el mismo agente con contexto de todo el equipo: *"¿qué está en riesgo esta semana?"*, *"¿quién está sobrecargado?"*, *"¿qué lleva más de 3 rondas de revisión?"*. Más un **digest diario** consolidado.

### Fase futura (diseñado, no construido en el MVP)

Verificación automática contra la evidencia: el conector Git ya trae commits y PRs enlazados; el conector de entregables ya sabe si se subió una v3. El agente los usa como respaldo en la conversación (*"veo un PR abierto en la 142"*, *"subiste el corte v2 ayer, ¿va a revisión?"*). El paso siguiente —opinar si la tarea está realmente cumplida— extiende el mismo enganche sin rediseñar nada.

---

## 5. Arquitectura

### Stack recomendado

| Capa | Elección |
|---|---|
| Frontend web | **React 19 + TypeScript + Vite**, TanStack Query, Tailwind + shadcn/ui, dnd-kit (Kanban), Zustand |
| Backend | **ASP.NET Core 9 (.NET 9)** — API REST + **SignalR** para tiempo real |
| Base de datos | **PostgreSQL 16** + EF Core (campos personalizados en `jsonb` con índices GIN) |
| IA | **SDK oficial de Anthropic para C#** (`dotnet add package Anthropic`), modelo `claude-opus-5` |
| Jobs | `BackgroundService` de .NET con tabla de jobs en Postgres |
| Agente escritorio | **.NET 9 WPF**, icono en bandeja + toast nativo, chat embebido con **WebView2** |
| Archivos | Disco local con capa `IFileStore` (S3/R2 después sin tocar el resto) |
| Email | SMTP vía MailKit |
| Deploy | Docker Compose: `api` + `postgres` + `caddy` (TLS automático) |

### Sobre Python

Dijiste que la IA podía ir en Python. **Mi recomendación es no hacerlo:** Anthropic publica SDK oficial de C# con `BetaToolRunner`, que es exactamente el bucle agéntico que necesitás (el modelo pide herramienta → se ejecuta → se devuelve el resultado → repite). Un servicio Python aparte suma un runtime más, un contenedor más, ~200 MB de RAM y un contrato HTTP interno que mantener, a cambio de nada que C# no te dé. Todo en .NET: un lenguaje, un despliegue, y vos lo mantenés cómodo. Si más adelante querés un framework de agentes de Python, `IAgentRunner` queda detrás de una interfaz y se reemplaza sin tocar el resto.

### Huella del servidor

Un VPS de **2 vCPU / 4 GB** sobra: API .NET ~250–400 MB, Postgres ~300 MB, Caddy ~20 MB. Compará con Plane (4 vCPU / 8 GB para 20 usuarios), Taiga (~4 GB) o Huly (8 GB mínimo). El frontend es estático servido por Caddy, no consume runtime.

### Diagrama

```
┌────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│ React (web)    │   │ Agente escritorio│   │ Email · Slack    │
│ Kanban+Dashboard│  │ (WPF + bandeja)  │   │ (escalera)       │
└───────┬────────┘   └────────┬─────────┘   └─────▲────────────┘
        │ REST + SignalR      │ SignalR            │
        └──────────┬──────────┘                    │
                   ▼                               │
        ┌──────────────────────────────┐           │
        │   ASP.NET Core 9             │           │
        │  ┌────────────────────────┐  │           │
        │  │ API REST + SignalR hub │  │           │
        │  │ INotificationChannel ──┼──┼───────────┘
        │  ├────────────────────────┤  │
        │  │ CheckInScheduler (BG)  │  │
        │  │ AgentRunner (Anthropic)│──┼──▶ Claude API
        │  │ IEvidenceConnector ────┼──┼──▶ Git / entregables / enlaces
        │  │ IIssueProvider ────────┼──┼──▶ nativo | GitHub Issues
        │  └────────────────────────┘  │
        └──────────────┬───────────────┘
                       ▼
                 PostgreSQL 16
```

### Modelo de datos (núcleo)

```
User(id, email, name, role, timezone, checkin_time, work_days, desktop_agent_token)
ProjectTemplate(id, name, workflow_def_json, field_defs_json, task_types_json, agent_context)
Project(id, name, key, template_id, workflow_id, settings, issue_provider)
Workflow(id, name) / WorkflowStage(id, workflow_id, name, order, category, is_blocked_stage)
CustomFieldDef(id, project_id, key, label, type, options_json, required, order)

Task(id, project_id, number, title, description_md, type, priority, status_id,
     assignee_id, reporter_id, estimate, progress_pct, due_date,
     custom_fields jsonb, external_ref, created_at, updated_at)
TaskLabel / TaskComment / TaskDependency
TaskEvent(id, task_id, actor_type[user|ai_agent|system], actor_id, field,
          old_value, new_value, check_in_id?, created_at)   ← historial + auditoría IA
Blocker(id, task_id, reason, blocked_by_user_id?, opened_at, resolved_at)

Deliverable(id, task_id, name, kind[file|link], current_version)
DeliverableVersion(id, deliverable_id, version, file_path|url, uploaded_by, created_at)
ReviewRound(id, task_id, deliverable_version_id, reviewer_id, status[pending|approved|changes],
            comments_md, opened_at, closed_at)

CheckIn(id, user_id, scheduled_at, status[pending|delivered|opened|partial|completed|missed],
        delivered_via, delivered_at, opened_at, completed_at, summary, transcript_json)
NotificationAttempt(id, check_in_id?, notification_id?, channel, step, sent_at,
                    delivered_at, opened_at, error)   ← la escalera, paso por paso
AgentAction(id, check_in_id, tool_name, arguments_json, status[applied|pending|rejected],
            approved_by, applied_at)
Notification(id, user_id, channel, payload_json, sent_at, read_at)
DeviceRegistration(id, user_id, token, app_version, last_heartbeat_at, os_info)
RepoLink(id, task_id, kind[commit|pr|branch], external_id, url, state, created_at)
```

`custom_fields` en `jsonb` con índice GIN da campos arbitrarios por proyecto sin migraciones ni tablas EAV. `Deliverable`/`ReviewRound` son el núcleo de los equipos creativos. `external_ref` y `RepoLink` son las bisagras hacia GitHub.

### Repositorio: los dos modos que pediste

`IIssueProvider` con implementaciones seleccionables **por proyecto** (un equipo puede tener un proyecto de diseño nativo y uno de desarrollo sobre GitHub):

- **`NativeProvider` (MVP, default).** Las tareas viven en tu DB. El repo se referencia por link y se conecta por **webhooks de solo lectura**: cada `push` o evento de PR que mencione `DEV-142` crea un `RepoLink`. Sin sincronización de issues, sin rate limits, y funciona con GitHub, GitLab, Bitbucket o ningún repo (que es el caso de diseño y edición).
- **`GitHubIssuesProvider` (fase 2).** Para equipos ya instalados en GitHub Issues: lectura/escritura vía GitHub App (tokens de instalación de corta vida, permisos finos, 15.000 req/h). El issue lo lee de GitHub; lo que GitHub no tiene —etapas custom, campos personalizados, estimaciones, progreso, check-ins— vive en tu DB indexado por `external_ref`. **Modelo híbrido: GitHub es dueño del issue, vos sos dueño del metadata de gestión.** Evita el sync bidireccional completo, que es la trampa cara (conflictos, deduplicación, colas, reintentos — típicamente el 40% del esfuerzo de un producto así).
- **`GitHubImporter` (fase 2, migración amigable).** Asistente: elegís repo, previsualizás el mapeo (labels → etiquetas, milestone → ciclo, state → etapa), importás conservando `external_ref`, y opcionalmente se comenta cada issue de GitHub con el link a TaskAdmin.

### El agente de escritorio

**Alcance deliberadamente mínimo: chat y notificaciones. Nada más.** No replica el tablero, ni
el detalle de tareas, ni la configuración. Mantener dos interfaces para lo mismo duplica el
trabajo y garantiza que una de las dos quede desactualizada.

- Cuando una notificación requiere una acción que excede el chat (revisar un entregable,
  aprobar algo, ver el tablero), muestra **un enlace que la abre en la web**.
- Hay siempre un **botón fijo para ir al sistema web**, sin importar en qué estado esté el chat.

App WPF de .NET 8 que arranca con Windows y vive en la bandeja del sistema:

- **SignalR** persistente con reconexión automática y heartbeat cada 60 s (el heartbeat es lo que decide si escala a email).
- **Toast nativo de Windows** al llegar un check-in, una @mención o una revisión pendiente.
- Ventana de chat con **WebView2** apuntando a `/agent-chat` de la app React → la UI de chat se escribe **una sola vez** y sirve para escritorio y web.
- Badge con tareas del día; menú contextual con "Abrir tablero", "Check-in ahora", "Pausar notificaciones".
- **Siempre corriendo:** autoarranque por la clave `Run` del registro del usuario (no requiere permisos de admin), instancia única por mutex nombrado, cerrar la ventana minimiza a bandeja en vez de salir, y reintento de conexión con backoff exponencial ante caída de red o suspensión del equipo. La app reporta su versión en el heartbeat, así el servidor sabe quién quedó atrás.
- Distribución: instalador MSIX o `.exe` self-contained. Presupuestá un **certificado de firma de código** (~US$200–400/año): sin él, cada instalación muestra el aviso de SmartScreen de Windows, que en un despliegue interno genera fricción y llamadas a soporte.
- Login por token de dispositivo emitido desde la web (un click en el perfil, se pega en la app).

### Garantía de entrega y de respuesta

Que el mensaje llegue y que la persona conteste son dos problemas distintos, y los dos necesitan mecanismo — no basta con mandar la notificación.

**Entrega — escalera automática.** El check-in es una máquina de estados con acuse de recibo, no un "fire and forget":

| Momento | Acción |
|---|---|
| T+0 | Toast de escritorio si hay heartbeat vivo |
| T+15 min sin abrir | Segundo toast, más insistente, y badge persistente en la bandeja |
| T+45 min sin abrir, **o sin heartbeat desde el inicio** | Email con enlace directo al chat (token de un solo uso, sin login) |
| T+2 h sin abrir | Segundo email + entrada en el feed del manager como "check-in sin entregar" |
| T+4 h (o fin de jornada) | Check-in marcado `missed` y contabilizado en la métrica de cobertura |

Cada paso se cancela apenas la app confirma **entrega** (`delivered_at`) y luego **apertura** (`opened_at`) — dos acuses distintos, porque "el toast salió" no es lo mismo que "lo vio".

**Respuesta — el problema difícil.** No se resuelve con presión técnica: si la conversación es larga o burocrática, la gente la abandona a la mitad. Los mecanismos que sí funcionan:

- **Persistencia de la conversación:** un check-in abierto a medias se retoma donde quedó, no vuelve a empezar. `CheckIn.status` distingue `pending`, `delivered`, `opened`, `partial`, `completed`, `missed`.
- **Salida rápida legítima:** botón "todo igual que ayer" que cierra el check-in en un click y registra la falta de cambios. Sin esa vía, la persona simplemente cierra la ventana y el dato se pierde.
- **Presupuesto de turnos:** el agente cierra en 5 turnos como máximo; si algo queda sin resolver, lo escala en vez de seguir preguntando.
- **Escalada blanda, no castigo:** tras dos check-ins perdidos seguidos, el manager ve la señal en su dashboard — el sistema informa, no sanciona.
- **Métrica de primera clase:** tasa de entrega y tasa de finalización por persona y por equipo, visibles desde el día 1. Si esta métrica cae, el producto no funciona, y hay que verlo antes de que lo note nadie más.

**Lo que el sistema no puede garantizar, y hay que decirlo:** con el equipo apagado, la app cerrada a la fuerza o el usuario decidido a ignorarlo, no hay canal que garantice una respuesta. La escalera asegura que *alguien se entere* de que no la hubo — que es lo accionable.

### Slack como canal opcional

Detrás de la misma interfaz `INotificationChannel` que usan Desktop y Email, así que agregarlo es aditivo.

**Sí, es gratis para lo que necesitás.** Crear una app de Slack, obtener un bot token y postear con `chat.postMessage` no tiene costo: la API no se cobra por llamada. Los límites a tener en cuenta en un workspace de plan gratuito:

- **Máximo 10 apps o integraciones por workspace** (cuenta cualquier app OAuth: GitHub, Drive, Zapier… y la tuya). Si el equipo ya tiene 10, hay que sacar una para entrar.
- **90 días de historial de mensajes.** Irrelevante para vos: el transcript autoritativo vive en tu `CheckIn.transcript_json`, Slack es solo el transporte.
- Rate limits de la API por método (del orden de 1 mensaje por segundo por canal), holgados para check-ins diarios.

**Cómo encaja:** con Slack el check-in se entrega como mensaje directo del bot y la persona responde en el hilo — el agente lee las respuestas por Events API y ejecuta las mismas herramientas. Es el canal indicado para el caso que mencionaste: equipos externos, personas sin Windows, o quien no puede instalar software en su máquina. **No reemplaza a la app de escritorio**, que sigue siendo el canal por defecto porque es el único con autoarranque garantizado y acuse de apertura confiable.

### Configuración: todo al alcance del admin

**Regla dura: si algo es configurable, tiene que poder configurarse desde la interfaz, en una
sección de Configuración visible para el rol Admin.** Nada de knobs que solo existen en
`appsettings.json` o en variables de entorno, porque eso obliga a tener acceso al servidor para
cambiar el horario de un check-in — y convierte cada ajuste en un ticket para vos.

Lo que vive en la base y se edita desde la UI:

| Grupo | Ajustes |
|---|---|
| Check-ins | Hora por defecto, días laborables, si se dispara solo cuando hay algo pendiente, umbral de "sin actualizar hace N días" |
| Escalera de entrega | Minutos hasta cada peldaño, tolerancia de heartbeat |
| Email | Servidor SMTP, puerto, credenciales, remitente, o modo "escribir a disco" |
| Modelo de IA | Proveedor (Anthropic / compatible con OpenAI / local), modelo, URL base, clave, esfuerzo, tope de turnos y de tokens |
| Almacenamiento | Tamaño máximo de archivo |
| Notificaciones | URL pública para los enlaces de los emails |

Las claves y contraseñas se guardan cifradas y **nunca se devuelven en las respuestas de la
API**: la UI muestra si están definidas, no su valor.

**Lo único que queda fuera, por necesidad técnica:** la cadena de conexión a la base y la clave
de firma de los JWT. Las dos se necesitan *antes* de poder leer la base, así que viven en
variables de entorno. Son dos, se tocan una vez al instalar, y están documentadas.

### Uso de Claude

- Modelo `claude-opus-5`, `thinking: adaptive`, `effort: "medium"` (bajo para conversaciones simples).
- **Prompt caching** sobre el prompt de sistema y las definiciones de herramientas (mínimo 512 tokens en Opus 5): el prefijo es idéntico en todos los check-ins, así que desde el segundo se paga ~10%. El contexto específico de la plantilla del proyecto va **después** del corte de caché.
- Contexto por check-in: prompt de sistema + `agent_context` de la plantilla + tareas de la persona con sus campos personalizados + últimos 3 check-ins + evidencia disponible.
- Todas las mutaciones pasan por herramientas con schema estricto (`strict: true`) — la IA nunca escribe SQL ni texto libre en campos estructurados.
- **Costo estimado:** ~$0.10–0.25 por check-in. Equipo de 20 personas × 22 días hábiles ≈ **$45–110/mes**.

---

## 6. Plan de construcción

> **Estado al 11/08/2026.** Fases 0 a 4 construidas y corriendo en Docker. El detalle verificado,
> lo pendiente y el cómo levantarlo están en el `README.md`, que es el documento vivo; este plan
> queda como la decisión de diseño original.
>
> **Decisiones que cambiaron respecto de lo escrito abajo**, y conviene leer antes de retomar:
>
> - **No hay lista de miembros de proyecto.** Participar es tener trabajo asignado; se deduce.
>   Lo único que se designa es quién lidera, y solo pueden liderar Admin o Manager.
> - **Cada uno mueve solo sus tareas**; el líder mueve todo, asigna y desasigna. Admin ve y hace
>   todo, en todos los proyectos, sin participar.
> - **El agente no le escribe a admins ni a líderes**: el check-in es para quien ejecuta el
>   trabajo. Al líder lo ayuda del otro lado, escribiéndole a sus responsables.
> - **Los mensajes directos** solo van a quien tiene trabajo en los proyectos que uno lidera.
> - **El check-in puede ser diario o solo cuando hace falta** (`CheckIns:Mode`): en el segundo
>   modo el agente aparece únicamente si hay algo vencido, bloqueado o sin novedades hace N días.
>   Es la versión que persigue el problema en vez de ritualizarlo.
> - **Los permisos del responsable son por tarea**, decididos al asignarla: mueve por defecto, no
>   edita ni borra salvo que se lo habiliten.
> - **La app de escritorio tiene login propio** y sesión que no se cierra (token de dispositivo);
>   sigue siendo solo chat y notificaciones.
> - **Todo corre en Docker**, incluido el build del frontend. La configuración es editable desde
>   la interfaz por el admin, salvo la cadena de conexión y la clave de firma.
>
> **Lo que falta de este plan:** digest diario y chat del manager (Fase 4), canal Slack (4b),
> conector Git (5), y probar la app de escritorio con interfaz gráfica.

### Fase 0 — Esqueleto (semana 1)
Solución .NET (`TaskAdmin.Api`, `.Domain`, `.Infrastructure`, `.Agent`), proyecto Vite+React, Docker Compose (api + postgres + caddy), EF Core con migración inicial, auth JWT con roles, seed con las 5 plantillas de proyecto.

### Fase 1 — Tracker usable en cualquier disciplina (semanas 2–5)
CRUD de proyectos desde plantilla, motor de workflow con transiciones válidas, **campos personalizados** (definición + formulario dinámico + filtros), CRUD de tareas, Kanban con drag & drop, lista filtrable, "mis tareas", comentarios, `TaskEvent` en cada cambio, notificaciones in-app por SignalR, dashboard del manager.
**Criterio de salida:** crear un proyecto de *Edición de video* y uno de *Desarrollo* en la misma instancia, cada uno con sus etapas, campos y vocabulario, sin escribir código.

### Fase 2 — Entregables y revisión (semana 6)
`IFileStore`, subida de entregables con versiones, enlaces externos, rondas de revisión con aprobar / pedir cambios, rol Cliente-revisor, formulario de intake público.
**Criterio de salida:** un diseñador sube v1, el cliente pide cambios, sube v2, el cliente aprueba — y queda registrado que llevó 2 rondas.

### Fase 3 — Canales y entrega garantizada (semanas 7–8)
`INotificationChannel` con `DesktopChannel` (SignalR) y `EmailChannel` (MailKit). App WPF de bandeja con WebView2, heartbeat, toasts, autoarranque por registro, instancia única y minimizar-a-bandeja. Registro de dispositivo por token. **Escalera de entrega** como máquina de estados con `NotificationAttempt` y doble acuse (`delivered_at` / `opened_at`). Panel de cobertura: tasa de entrega y de finalización por persona.
**Criterio de salida:** con la app abierta llega el toast y el servidor registra apertura; con la app cerrada, a los 45 min llega el email; sin respuesta a las 2 h, el manager lo ve en su feed.

### Fase 4 — El agente (semanas 9–11) ← el corazón del producto
`AgentRunner` sobre el SDK de C# con `BetaToolRunner`, las 9 herramientas con schema estricto, `CheckInScheduler` respetando timezone y días laborables, contexto por plantilla, UI de chat en React, tabla `AgentAction` con cola de aprobaciones, digest diario, chat del manager.
**Criterio de salida:** a las 9:00 una editora y un backend reciben un toast cada uno, conversan 4 turnos **en el lenguaje de su oficio**, y ambos tableros quedan actualizados sin que nadie toque un formulario.

### Fase 4b — Canal Slack (semana 12, opcional)
`SlackChannel` sobre la misma interfaz: app de Slack con bot token, entrega por mensaje directo, respuestas leídas del hilo vía Events API, mismas herramientas del agente. Para equipos externos, personas sin Windows, o quien no puede instalar software.

### Fase 5 — Conector Git (semana 13)
Webhooks de GitHub con verificación de firma, parser de `[A-Z]+-\d+` en commits y PRs, `RepoLink`, botón "copiar nombre de rama", evidencia inyectada en el contexto del check-in.

### Fase 6 — Después
`GitHubIssuesProvider`, asistente de importación, ciclos y épicas, calendario, carga por persona, cycle time y rondas promedio, verificación automática de tareas contra la evidencia.

---

## 7. Riesgos y cómo se mitigan

| Riesgo | Mitigación |
|---|---|
| **La persona ignora el check-in** (mata el producto) | Escalera de entrega con doble acuse y cuatro peldaños hasta el feed del manager; conversación de 30 segundos con salida en un click ("todo igual que ayer"); el agente abre con datos concretos, no con "¿cómo vas?"; tasa de entrega y de finalización visibles desde el día 1 |
| **La app de escritorio no está corriendo** | Autoarranque por registro, instancia única, minimizar-a-bandeja en vez de cerrar, reconexión con backoff; y si aun así no hay heartbeat, la escalera cae a email sin esperar el timeout de apertura |
| **Fricción al instalar (SmartScreen)** | Certificado de firma de código presupuestado desde el inicio (~US$200–400/año); sin firma, cada instalación genera un aviso de Windows y una llamada a soporte |
| **Genérico = mediocre en todo** | La generalidad va en datos (plantillas, campos, conectores), no en abstracciones vagas de UI. Cada plantilla se valida con un equipo real de esa disciplina antes de darla por buena |
| **La IA rompe el tablero** | Herramientas con schema estricto, transiciones validadas en el dominio y no en el prompt, fechas y trabajo nuevo con aprobación humana, todo reversible y auditado |
| **Se percibe como vigilancia** | Tono de asistente, no de supervisor; cada persona ve su propio transcript; el resumen al manager es de estado de tareas, no de evaluación de la persona |
| **Costo de IA impredecible** | Prompt caching, `effort` bajo, tope de turnos y de tokens por conversación, dashboard de gasto |
| **Ámbito que se desborda** | El MVP no lleva ciclos, épicas, calendario ni permisos finos. Se agregan cuando el diferenciador ya esté probado |

---

## 8. Verificación

- **Generalidad (la prueba clave):** en una misma instancia, crear un proyecto de *Desarrollo* y uno de *Edición de video*; confirmar que cada uno tiene sus etapas, sus campos personalizados, su prefijo de ID y su vocabulario, y que ninguna pantalla muestra jerga de la otra disciplina.
- **Tracker:** crear tarea → asignar → recorrer las etapas → confirmar que cada transición inválida se rechaza y que cada cambio queda en `TaskEvent`.
- **Campos personalizados:** definir un campo `Duración (seg)` numérico en el proyecto de video, filtrar la lista por él y verificar que el índice GIN lo resuelve sin escaneo secuencial.
- **Revisión:** subir v1 → cliente pide cambios → v2 → aprobar; verificar que quedan 2 `ReviewRound` y que el entregable apunta a la versión aprobada.
- **Tiempo real:** dos navegadores abiertos, mover una tarjeta en uno y verla moverse en el otro.
- **Canales y escalera:** con la app de bandeja abierta, disparar una notificación, ver el toast y confirmar que el servidor marca `delivered_at` y luego `opened_at` al abrirla. Cerrar la app, disparar de nuevo, y verificar los cuatro peldaños con los tiempos acortados en configuración: segundo toast, email, segundo email + feed del manager, y `missed`. Confirmar que abrir el chat en cualquier peldaño cancela los siguientes.
- **Siempre corriendo:** reiniciar Windows y verificar que la app levanta sola; matar el proceso de red (o desconectar) y confirmar reconexión con backoff; abrir la app dos veces y confirmar que el mutex impide la segunda instancia; presionar la X y confirmar que minimiza a bandeja en vez de cerrar.
- **Retomar a medias:** empezar un check-in, cerrar la ventana al segundo turno, reabrir y confirmar que la conversación continúa donde quedó con `status = partial`.
- **Agente (el test que importa):** sembrar dos personas de disciplinas distintas con 3 tareas cada una —una atrasada, una bloqueada, una al día—, forzar los check-ins, responder *"la 142 la termino mañana, la 150 sigue trabada esperando a Pedro"* y *"el corte v2 está listo, falta el color"*, y verificar que: se registra avance, se propone la nueva fecha (pendiente de aprobación), se abre el `Blocker` apuntando a Pedro, se actualiza el campo `versión de corte`, se alerta al manager, y cada `AgentAction` queda trazada al `check_in_id`.
- **Git:** push con `DEV-142` en el mensaje → aparece el `RepoLink`; abrir PR → se enlaza y refleja estado.
- **Peso:** `docker stats` con 20 usuarios simulados por debajo de 1,5 GB en total.

---

## 9. Multi-organización (diseñado, no construido)

Hasta acá el plan asume **una instancia = una empresa**: el primer admin nace de `Bootstrap__AdminEmail`, no hay registro, y todos los proyectos son de todos. Esta sección lo abre a varias empresas sobre el mismo despliegue, como hacen GitHub, GitLab y Azure DevOps.

### La entidad que faltaba: `Organization`

Lo que agrupa proyectos es una **organización**, no una persona. La distinción no es cosmética: si los proyectos cuelgan de la cuenta de quien se registró, esa persona no se puede ir de la empresa sin que sea un incidente, no hay forma de tener dos dueños, y la facturación y el dominio de email no tienen dónde vivir.

Quien se registra **crea la organización** y queda como su primer `Owner`.

### Tres niveles de autoridad, no dos

| Nivel | Quién es | Alcance |
|---|---|---|
| **Operador de plataforma** | Quien hospeda TaskAdmin | La lista de organizaciones: altas, suspensión, métricas de uso. **No** el contenido de los proyectos |
| **Owner / Admin de organización** | Quien abrió la cuenta de su empresa | Todo lo de *su* organización. Es el `Admin` de hoy, acotado |
| **Manager / Colaborador / Cliente** | Los roles ya existentes | Igual que hoy, dentro de su organización |

El operador **no ve el contenido** de las organizaciones. Para soporte se agrega un acceso explícito, con consentimiento del Owner, vencimiento y traza en `WorkItemEvent`. Un botón de "ver todo" sin auditoría es lo primero que objeta cualquier empresa que evalúe el producto.

Fundir los dos primeros niveles en un solo rol haría imposible expresar «este admin no puede ver la empresa de al lado», que es justamente la garantía que el modelo tiene que dar.

### Decisiones tomadas

1. **Una cuenta por organización** (modelo Slack, no GitHub). Si la misma dirección trabaja para dos empresas, son dos cuentas. El JWT lleva una sola organización, el filtro de datos es directo y no hace falta selector de contexto en la UI. `User` conserva sin cambios su perfil de trabajo (zona horaria, hora de check-in, dispositivos), que es lo que se rompería si la identidad fuera global.
2. **El alta de organización es solo con Google** (OIDC). Aprovecha el flujo con PKCE ya construido y evita tener que armar verificación de email desde cero, que es lo que hace falta para que el registro con contraseña no llene la base de organizaciones fantasma. El alta de *miembros* dentro de una organización sigue como hoy: la crea un Admin, con contraseña o vinculada al proveedor.
3. **Modelo de IA y SMTP: la plataforma pone los suyos, la organización puede reemplazarlos.** Arranca funcionando el primer día sin configurar nada, y quien quiera —o quien despliegue on-premise contra un Ollama local— pone los propios y deja de consumir los de la plataforma.

### Aislamiento de datos

`OrganizationId` en **todas** las tablas, no solo en las raíces, con un *global query filter* de EF alimentado por el claim del JWT. Es redundante —`WorkItem` podría deducirlo por su `Project`— y esa redundancia es el punto: un solo `Include` o un `Any()` mal escrito sin la columna es una fuga de datos entre empresas, y el filtro global no depende de que cada consulta se acuerde.

Consecuencias concretas sobre el esquema actual:

- `User.Email` y `Project.Key` pasan de únicos globales a únicos **por organización** (`TaskAdminDbContext.cs`, líneas 53 y 120). Así dos empresas pueden tener cada una su proyecto `DEV`.
- `ProjectTemplate.Key`, `Label` y los contadores de `NextItemNumber` quedan igualmente por organización.
- `IntakeToken` y los tokens de dispositivo siguen siendo únicos globales: son secretos, no nombres.
- La migración asigna todo lo existente a la organización #1, que es la instancia actual.

### Configuración por organización

Hoy `AppSettings` se inyecta como fuente de `IConfiguration` del proceso (`DatabaseConfigurationSource`): un diccionario único para toda la app, cargado una vez. Sirve para una instancia, y es incompatible con overrides por empresa — el Ollama de una sería el de todas.

El catálogo se parte en tres:

- **De plataforma, no editable por las organizaciones:** `Jwt:*`, `Oidc:*` (el login con Google es uno solo), `Storage:MaxBytes`, `Notifications:PublicBaseUrl`. Siguen en `IConfiguration` como hoy.
- **De organización, con valor por defecto de la plataforma:** `AgentModel:*` y `Email:*`. Dejan de leerse de `IConfiguration` y pasan a un resolvedor por organización, que devuelve el valor propio si existe y el de la plataforma si no. Los secretos se siguen cifrando con `SecretProtector`.
- **De organización, sin defecto compartido:** `CheckIns:*`, `Defaults:*` y los peldaños de `Notifications:*`. Son decisiones de cada equipo.

Con la clave de la plataforma en juego hace falta además **medir consumo por organización** —tokens por `AgentAction`— o el costo del agente es un número sin dueño.

### On-premise: un interruptor, no una bifurcación

`Tenancy:Mode = Single | Multi`.

En `Single` no existe el registro ni la consola de plataforma, hay una organización implícita y el bootstrap funciona exactamente como hoy. Es **el mismo código con un switch**, nunca una rama aparte: bifurcado, en tres meses la variante on-premise tiene bugs que la otra ya arregló.

### Etapas

Ninguna deja la app a medio funcionar.

- **9a — Esquema y aislamiento. ✅ Construido y verificado.** Entidad `Organization`, `OrganizationId` en las 26 tablas, filtro global, claim en el JWT, migración que mete lo existente en la organización #1. Con una sola organización, la app se comporta idéntico.

  Una trampa que costó encontrar y que conviene no volver a pisar: si el filtro global lee la organización de una propiedad **estática**, EF la considera constante, la incrusta en el SQL y cachea esa consulta —y entonces la segunda organización recibe el SQL de la primera y ve sus datos—. Tiene que leerla de una propiedad **de instancia del DbContext** para que EF la extraiga como parámetro (`@__ef_filter__…`) y la relea en cada ejecución. Esa propiedad delega en un `AsyncLocal` estático, porque el modelo se construye una sola vez y la expresión queda atada a la instancia que existía entonces.

- **9b — Roles y consola de plataforma. ✅ Construido.** Interruptor `Tenancy:Mode`, rol `PlatformOperator` con su propia organización (`IsPlatform`), consola en `/plataforma` para listar, crear, suspender y reactivar. El acceso de soporte auditado sigue pendiente y es deliberado: mientras no exista, la promesa de que nadie mira adentro no tiene excepciones que explicar.
- **9c — Alta de organización. ✅ Construido, por dos caminos.** Con el proveedor: el callback OIDC, ante un email desconocido y con el registro abierto, deja el intento pendiente en vez de rechazarlo; el frontend pide una sola cosa —el nombre de la empresa— y crea organización, Owner y plantillas. La cuenta nace sin contraseña, porque entró con el proveedor y va a seguir entrando por ahí. Con email y contraseña: `PendingRegistration` + correo de confirmación, y la organización se crea recién al hacer clic. La verificación no es opcional acá — sin ella el formulario es una fábrica de empresas fantasma. El alta está enlazada desde la pantalla de entrada: un camino que existe y no se ve es un camino que no existe.
- **9d — Configuración por organización. ✅ Construido.** Catálogo partido por alcance (`SettingsCatalog.ScopeOf`), tabla `OrganizationSettings` con herencia de la plataforma, resolvedor de `AgentModel` y `Email` por organización, y consumo de tokens de 30 días por empresa en la consola del operador.

### Verificación

- **Aislamiento (la prueba que importa):** dos organizaciones con un proyecto `DEV` cada una. Confirmar que el Admin de A no ve ni un work item, usuario, entregable, mensaje directo ni notificación de B — incluidos los endpoints que buscan por ID directo, no solo los listados.
- **Contadores:** crear tareas en paralelo en el `DEV` de A y el de B; confirmar `DEV-1` en las dos, sin huecos ni duplicados.
- **Alta:** entrar con una cuenta de Google desconocida, crear organización, y verificar que arranca con las plantillas semilla y sin ningún dato de otra empresa.
- **Modelo:** apuntar el `AgentModel:BaseUrl` de A a un Ollama local y confirmar que un check-in de B sigue usando el modelo de la plataforma.
- **On-premise:** con `Tenancy:Mode=Single`, confirmar que la ruta de registro devuelve 404 y que el bootstrap crea el admin igual que antes.

---

## 10. Tres idiomas: español, inglés y portugués

### Decisiones

- **El idioma es de cada persona, no de la organización.** Sale del navegador y se puede cambiar con un selector; la elección se guarda en `User.Language` y desde ese momento manda por encima del navegador. Un idioma por empresa sería más simple de construir y deja incómodo al diseñador brasileño de una empresa argentina, que es exactamente el caso que este producto va a encontrar.
- **Se guarda en el servidor y no solo en el navegador.** Los emails de la escalera se arman del lado del servidor, sin navegador del cual deducir nada: sin esa columna, quien usa la app en portugués recibiría los avisos en castellano.
- **El agente conversará en el idioma de cada persona.** Decidido, no construido: implica traducir el prompt del sistema y los contextos de las cinco plantillas, que son textos largos y con matiz de oficio.

### Cómo está armado

- **Diccionarios en código**, no `.resx` ni `i18next`. En el frontend, `web/src/locales/{es,en,pt}.ts` con claves planas; el castellano es el diccionario de referencia y los otros dos se tipan contra él, así que **una traducción que falta es un error de compilación**. En el servidor, `Messages.cs` con el mismo criterio. Son unas pocas centenas de frases: una biblioteca de i18n agregaría un formato de archivo, un pipeline y un vocabulario nuevo para resolver un problema que a esta escala no existe. Si algún día entra un equipo de traducción, mudarlo es mecánico.
- **Sin traducción, cae al castellano** en vez de mostrar la clave cruda: una frase en otro idioma se entiende, `settings.model.title` no.
- **El idioma viaja en cada request**: en el claim `lang` del token cuando hay sesión, y en `Accept-Language` cuando no la hay —el alta y el login, que son justo donde peor cae un error en un idioma ajeno—.
- Los **mensajes de log siguen en castellano**: los lee quien opera el servidor, no el usuario, y traducirlos haría más difícil buscarlos.

### El inglés es el idioma de reserva

Cuando no se puede determinar el idioma —un navegador configurado en algo que no hablamos, un `Accept-Language` ausente— se contesta en inglés. Y si a un diccionario le faltara una frase, esa frase sale en inglés en vez de mostrar la clave. Por eso el inglés es además el **diccionario de referencia**: los otros dos se tipan contra él, así que no puede faltarle una clave a nadie sin que el compilador lo diga.

### Estado

✅ **Construido y verificado.** El mecanismo completo, el selector (también en las pantallas públicas), la columna `User.Language`, el endpoint que la guarda y el claim que la transporta. **Las 19 pantallas y componentes de la interfaz**, más los mensajes del servidor de login, dispositivo y alta y los dos correos del registro. **577 claves × 3 idiomas.**

Cómo quedó resuelto el catálogo de ajustes: sus etiquetas y ayudas viven en el servidor y siguen llegando en castellano. La pantalla las traduce por la clave del ajuste —`AgentModel:Model` → `settings.AgentModel:Model.label`— y, si no encuentra traducción, muestra la que vino del servidor. Así agregar un ajuste nuevo nunca deja la pantalla rota: en el peor caso se lee en castellano hasta que alguien lo traduzca, en vez de mostrar una clave cruda. La alternativa —mover esas sesenta cadenas al servidor— habría significado mantener allá una segunda máquina de traducción.

⏳ **Pendiente:** el agente, según la decisión de arriba, y los **datos semilla** —los nombres y etapas de las cinco plantillas de fábrica, que se siembran en castellano—. Son datos de la base y no cadenas de la interfaz: traducirlos es sembrarlos en el idioma de la organización al crearla, no un diccionario más.

---

## 11. El agente sobre un modelo local

**La decisión:** la instalación corre en la máquina de casa, expuesta por un túnel de Cloudflare, y el agente usa un modelo que corre ahí mismo. No es por costo —con veinte personas la API sale unos pocos dólares al mes, menos que un VPS— sino porque **el hardware ya está pago y los datos no salen**, que es además el argumento de venta on-premise.

**Por qué un MoE.** El equipo es un Ryzen 9 6900HX con gráficos integrados: sin CUDA, el modelo corre en CPU, y ahí un modelo denso de 24–30B da 3–5 tokens/s, inusable para conversar. **Qwen3-Coder-30B-A3B** activa 3B de sus 30B por token: rinde como uno chico y responde como uno grande. Medido: **4 a 7 s por turno**.

**Q3 le gana a Q4, y no era lo esperado.** Con parámetros idénticos, sobre los casos donde el agente decide entre escribir y preguntar, el Q3 acertó 9/9 y el Q4 6/9 — fallando **3 de 3** el caso de la tarea que no existe, donde le colgó el bloqueo a otra tarea. El Q4 es más decidido y actúa; el Q3 duda y pregunta. Acá dudar es lo correcto: un dato omitido lo repite la persona, un bloqueo falso en el tablero lo lee alguien que no estuvo en la charla. El Q4 además ocupa 20 GB y voltea a Docker; el Q3 convive en 15.

**Dos trampas que fallan en silencio.** Un modelfile sin `RENDERER`/`PARSER` deja al modelo emitiendo sus llamadas como texto: conversa bien y el tablero no se actualiza nunca. Y sin `num_ctx` declarado, Ollama usa 4096 tokens y el modelo pierde las definiciones de herramientas. Ninguna de las dos da error.

**Lo que el prompt tuvo que absorber.** El banco de pruebas mostró tres conductas que ninguna cuantización arregla y sí arregla el prompt: contestar con paredes de texto, repetirle a la persona su propio tablero, y —la peor— **redactar antes de leer**: narrar una tarea inexistente en el mismo turno en que se llama a `get_assigned_tasks`. Se agregaron reglas explícitas para las tres, más qué hacer cuando la persona menciona trabajo que no está en el tablero (preguntar, o proponerlo con `create_followup_task`).

✅ **Construido y verificado de punta a punta**: navegador → API en Docker → Ollama en el host → herramientas → Postgres. Con el tablero vacío el agente dice que no hay tareas; ante un «lo de ayer quedó a medias» admite que no sabe de cuál se trata y repregunta.

✅ **El despliegue casero, construido.** `deploy/respaldo.ps1` (volcado `custom` de Postgres más un tar de `uploads`, con retención y verificación de que el tar se lea — probado de verdad, no solo escrito) y `deploy/instalar-servidor-casero.ps1`, que lo programa a diario, apaga la suspensión y pone a Docker a arrancar con la sesión. La tarea corre como el usuario y no como SYSTEM: el daemon de Docker Desktop vive en la sesión, y una tarea de SYSTEM fallaría todas las noches en silencio. El túnel queda documentado en el README porque depende del dominio.

⏳ **Pendiente:** copiar los respaldos fuera de la máquina, y las horas activas de Windows Update.

---

## 12. El relevo: responsable por etapa

**El problema que resuelve.** La escalera de entrega detecta que *una persona no hizo su check-in*. No detecta que *una tarea llegó a Edición y nadie se enteró* — y ese es el hueco caro, porque es invisible: la tarea figura avanzando, nadie la reclama, y se descubre cuando algo vence.

**La decisión de modelo: el responsable es de la etapa, no de la tarea.** Un responsable por etapa *en cada tarea* obliga a llenar tantos campos como etapas tenga el workflow, cada vez que alguien crea una tarjeta. Nadie lo sostiene; a la semana los datos están podridos y las notificaciones van a la persona equivocada, que es peor que no tenerlas. En la etapa se configura **una vez** al armar el proyecto y cada tarea lo hereda al pasar. La excepción no necesita modelo: quien quiera otra persona en una tarea puntual la reasigna a mano, y esa asignación manda.

`WorkflowStages.DefaultAssigneeId`, nulo por defecto. Nulo significa «acá el trabajo no cambia de manos», que es lo correcto para etapas de tránsito como «Bloqueado».

**La regla del aviso: se avisa cuando cambia de manos, no cuando cambia de estado.** Si la etapa destino tiene el mismo responsable que ya tenía la tarea, no se dice nada: la persona acaba de hacerlo y contarle lo que ya sabe es la forma más rápida de que aprenda a ignorar los avisos que sí importan.

**Y un relevo no es un check-in**, así que no reusa la escalera. Un check-in insiste, sube a email y termina avisándole al responsable, porque su falta de respuesta significa algo. Un relevo dice «te llegó esto»; que no lo abras en 45 minutos significa que estás trabajando en otra cosa. Aviso plano, sin peldaños.

**El modo de falla que se cubrió explícitamente.** Si el responsable de una etapa queda inactivo, el trabajo se apilaría en silencio sobre una cuenta muerta. En vez de eso la tarea queda **sin asignar** —visible en el tablero— y se les avisa a los administradores. Un ruteo automático que falla callado es peor que el manual.

**Dónde se configura, y por qué ahí.** En el encabezado de cada columna del tablero, no en una pantalla de ajustes aparte. Es donde aparece la pregunta: se mira el tablero, se ve que «Edición» no tiene a nadie, y se resuelve en el acto. Escondido en un formulario de configuración, nadie lo completaría — y un relevo mal configurado es peor que ninguno, porque manda el aviso a la persona equivocada.

Quien no puede repartir trabajo **lo ve pero no lo edita**: enterarse de a quién le va a llegar lo que uno termina no es un privilegio de administración.

✅ **Construido y verificado**: se configuró un responsable en «Redacción», se movió una tarea de otra persona desde fuera del navegador, y cambió de dueño sola.

⏳ **Pendiente:** agrupar los avisos. Si a alguien le caen cuatro tareas en diez minutos hoy recibe cuatro globos, y debería ser uno.

## 13. Tiempo real en la interfaz

Como GitLab o Azure DevOps: si otra persona —o el agente— mueve algo, aparece en la pantalla del resto sin refrescar.

**Se difunde el hecho, no el estado.** El evento dice «se movió DEV-142», no la tarea entera. Mandar el objeto obligaría a serializarlo igual que la API y a mantener dos formas del mismo dato sincronizadas para siempre; en vez de eso el cliente invalida su consulta y vuelve a pedir lo que ya sabe pedir — con sus permisos aplicados, así que un evento no puede filtrarle a nadie algo que no podría ver por HTTP.

**Grupos por proyecto y no por organización.** Dentro de una empresa no todos ven todos los proyectos, y difundir a la organización entera filtraría qué proyectos existen y qué se mueve en ellos. La suscripción se pide desde el cliente y **el permiso se comprueba en el servidor**: `WatchBoard` verifica el acceso antes de sumar la conexión al grupo.

**El aviso se emite en el servicio de dominio, no en los endpoints.** Si viviera en la API, un cambio hecho por el agente —que no pasa por HTTP— no llegaría a ninguna pantalla. Y siempre después del `SaveChanges`: avisar antes deja al cliente pidiendo el tablero y leyendo el estado viejo.

✅ **Construido y verificado**: con el tablero abierto en el navegador, una tarea movida desde un proceso externo cambió de columna, de dueño y recalculó los contadores del panel lateral, sin refrescar.

---

## 14. La organización se ve: nombre y logo

Hasta acá una persona entraba y no había ningún indicio de en qué empresa estaba. En una instalación de una sola organización eso se tolera; en una que aloja a varias es un problema real —sobre todo para quien tiene cuenta en más de una— y además le quita a la plataforma cualquier sensación de ser «de uno».

**El nombre y el logo viajan con la sesión.** La respuesta de login trae `organizationName`, `organizationId` y `logoPath`, y el encabezado los dibuja. `logoPath` está para saber *si hay* logo sin tener que pedirlo: sin ese dato, cada arranque haría un pedido que devuelve 404 para toda organización que no cargó uno.

**El logo se carga al crear la organización**, en los tres caminos que la crean: la consola del operador, el registro con email y el paso siguiente al proveedor externo. Los tres comparten `LogoUpload`, que es el único lugar donde se valida —imagen, 2 MB— para que la regla no se triplique y quede desalineada. El nombre de archivo que manda el cliente nunca toca el disco: lo maneja `IFileStore`.

**El alta con email guarda el logo antes de saber si va a haber alta.** Es inevitable: el endpoint no puede decir si la dirección ya tiene cuenta sin filtrar esa información, así que responde lo mismo en los dos casos. La consecuencia es que hay caminos donde el logo queda escrito y la organización no se crea, y por eso cada uno de ellos lo borra explícitamente. Un logo huérfano no rompe nada, pero llena el disco de a poco y sin ruido.

**El logo se sirve con sesión, no como archivo público** — las organizaciones no se ven entre sí, y su logo tampoco. Como un `<img>` no puede mandar el header de autorización, el frontend lo trae con `fetch` y lo convierte en una URL de blob.

### La comprobación que faltaba

El endpoint pedía sesión, pero no que la sesión fuera **de esa** organización. `Organization` no implementa `IOrganizationScoped` —es la organización, no algo que le pertenezca—, así que el filtro global que separa a las empresas no cubre esa tabla, y nadie más lo suplía. Cualquier persona con sesión podía pedir el logo de otra empresa sabiendo su id, y la diferencia entre 200 y 404 ya confirmaba que ese id existe.

Corregido: **la propia, o cualquiera si sos operador** —que es quien las lista en su consola—. Es el mismo patrón que hay que recordar cada vez que se agregue un endpoint sobre `Organizations`: es la única tabla del sistema donde el aislamiento no viene solo.

### La marca del encabezado es la empresa, no la plataforma

El nombre del software estaba primero y grande, y la organización al lado en gris chico. Está al revés de cómo se usa: quien abre esto todos los días trabaja para su empresa, y «TaskAdmin» es un dato de quién proveyó el software —además de un nombre provisorio—.

Ahora la barra abre con el **logo de la organización a 36 px y su nombre a 16 px semibold**; la marca de la plataforma quedó como texto de 11 px, apagado y sin enlace, contra el borde derecho. En la consola del operador sigue mandando «TaskAdmin»: el operador no pertenece a ninguna empresa, así que no hay logo que poner en su lugar.

**Una organización sin logo dibuja su inicial** sobre un fondo neutro, del mismo tamaño que ocuparía el logo. Sin eso, la cabecera de quien no cargó ninguno se veía rota en vez de sobria — y cambiaba de forma según el caso.

✅ **Construido y verificado** midiendo el DOM: logo de 16 → 36 px, nombre de 14 px apagado → 16 px semibold, marca de plataforma a 11 px contra el borde derecho, y el monograma ocupando exactamente los mismos 36 px cuando no hay logo.

⏳ **Pendiente:** cambiar el logo después del alta. Hoy se carga al crear y no vuelve a tocarse; quien quiera cambiarlo no tiene por dónde.

---

## 15. Borrar una organización

Suspender cubre el caso normal —cortar el acceso por falta de pago o por abuso, sin perder nada— pero no cubre dos: la prueba que quedó dando vueltas, y la empresa que se va y pide que sus datos no queden. Para eso hace falta borrar de verdad.

**Tres barreras, y cada una tapa un error distinto.**

| Barrera | Qué error evita |
|---|---|
| Hay que suspenderla antes | El impulso. Para llegar al botón hubo que cortarle el acceso primero y ver qué pasaba |
| Hay que escribir el nombre exacto | El clic en la fila equivocada, que en una lista de nombres parecidos es fácil |
| La organización de la plataforma nunca | Dejar la instalación sin nadie que pueda administrarla, y sin forma de arreglarlo desde la interfaz |

**El orden de borrado y por qué es frágil a propósito.** Las 27 tablas que apuntan a `Organizations` lo hacen con `ON DELETE RESTRICT`, así que hay que vaciarlas de hijas a madres, en una lista explícita. Una tabla nueva que nadie agregue a esa lista queda afuera —y entonces el borrado de la organización falla por la clave foránea y la transacción entera se deshace—. Es decir: **el `RESTRICT` que obliga a la lista es también lo que impide un borrado a medias.** Falla ruidosamente y sin tocar nada, que es el modo de falla que se quiere en algo irreversible.

**Los archivos se borran después de que cierre la transacción.** Las rutas se leen antes —después no hay de dónde sacarlas—, pero el borrado en disco va al final: si se borraran adentro y la transacción fallara, los archivos ya no estarían y las filas seguirían apuntándolos. Al revés, un archivo que no se pudo borrar solo ocupa disco.

**Y queda en el log del servidor**, porque en la base no queda nada: borrada la organización se fue también cualquier registro que hubiéramos escrito adentro suyo.

✅ **Construido y verificado**: las tres barreras rechazan como corresponde, una organización real se borró con su archivo, y una consulta de filas huérfanas sobre seis tablas devolvió cero.

⏳ **Pendiente:** exportar antes de borrar. Hoy la única forma de conservar algo es el respaldo de la base.

---

## Fuentes

- [Best Bug Issue Tracking Software, Ranked for 2026 — Gitnux](https://gitnux.org/best/bug-issue-tracking-software/)
- [Linear vs Jira 2026 — PromptedDev](https://prompteddev.com/blog/linear-vs-jira/)
- [Jira AI Agents as Team Members: Atlassian's Feb 2026 Launch — byteiota](https://byteiota.com/jira-ai-agents-as-team-members-atlassians-feb-2026-launch/)
- [Linear Review 2026 — utilo](https://utilo.io/blog/linear-review-2026-project-management)
- [Plane vs Huly vs Taiga: Self-Hosted Project Management 2026 — Pi Stack](https://www.pistack.xyz/posts/plane-vs-huly-vs-taiga-self-hosted-project-management-guide-2026/)
- [The definitive guide to self-hosted project management in 2026 — Plane](https://plane.so/blog/self-hosted-project-management-jira-server-alternative)
- [The best standup bots in 2026 — Product Hunt](https://www.producthunt.com/categories/standup-bots)
- [Best Async Standup Tools — Steady](https://runsteady.com/best-async-standup-tools/)
- [Asana vs Monday vs ClickUp 2026 — TrackingTime](https://trackingtime.co/project-management-software/asana-vs-monday-vs-clickup.html)
- [ClickUp vs Notion vs Asana vs Monday.com: AI Features 2026 — TaskRhino](https://www.taskrhino.ca/blog/notion-vs-monday-com/)
- [Best Production Tracking Tools for Creative Teams 2026 — Automateed](https://www.automateed.com/project-management-tools-for-creators)
- [Usage limits for free workspaces — Slack](https://slack.com/help/articles/115002422943-Usage-limits-for-free-workspaces)
- [Feature limitations on the free version of Slack — Slack](https://slack.com/help/articles/27204752526611-Feature-limitations-on-the-free-version-of-Slack)
- [Best practices for using webhooks — GitHub Docs](https://docs.github.com/en/webhooks/using-webhooks/best-practices-for-using-webhooks)
