# Borlaro TMS — Gestión de trabajo con agente de IA de seguimiento

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
- **`GitHubImporter` (fase 2, migración amigable).** Asistente: elegís repo, previsualizás el mapeo (labels → etiquetas, milestone → ciclo, state → etapa), importás conservando `external_ref`, y opcionalmente se comenta cada issue de GitHub con el link a Borlaro TMS.

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

| Momento | Peldaño | Por dónde |
|---|---|---|
| T+0 | `FirstDirectPing` | El canal personal de esa persona |
| T+15 min sin abrir | `SecondDirectPing` | El mismo, más insistente |
| T+45 min sin abrir, **o sin ningún canal personal disponible desde el inicio** | `FirstEmail` | Correo, con enlace directo al chat |
| T+2 h sin abrir | `SecondEmailAndManagerFeed` | Correo + entrada en el feed del líder |
| T+4 h | `MarkedMissed` | Se marca `missed` y cuenta en la métrica de cobertura |

**Los peldaños nombran un rol, no un canal**, y eso es deliberado. Se llamaban `FirstDesktopToast` y `SecondDesktopToast`, y ese nombre ataba la escalera a la app de bandeja —que es de Windows—. Con equipos donde el proyecto de video trabaja en Mac y el de desarrollo está mezclado, la escalera empezaba por un canal que para media empresa no existía.

Ahora los dos primeros peldaños se resuelven contra **los canales personales de la persona**: todo lo que no sea correo ni la campana de la aplicación. Se prueban en orden —el preferido primero, si lo fijó— y se usa el primero que conteste que puede entregar ahora. `Users.PreferredChannel`, nulo por defecto, es para quien tiene dos y quiere que le lleguen por uno.

**El correo no se elige como preferido**, y el endpoint lo rechaza: no es un canal personal sino el último recurso de todos, el único que no exige haber instalado ni vinculado nada. Dejarlo elegir sería saltearse los dos primeros peldaños sin querer.

**Si ningún canal personal puede entregar** —nadie con la app abierta, nadie con Slack vinculado—, se salta directo al correo en vez de gastar los 15 y 45 minutos esperando un acuse imposible. Y cada canal que no pudo entregar deja su propio registro: «no le llegó» y «no había por dónde» son dos historias distintas cuando alguien pregunta al día siguiente.

Cada paso se cancela apenas hay **entrega** (`delivered_at`) y luego **apertura** (`opened_at`) — dos acuses distintos, porque "el aviso salió" no es lo mismo que "lo vio". **Solo el escritorio tiene acuse de apertura de verdad**; el resto se cuenta como entregado al mandarlo, y lo que sigue faltando es que la persona lo abra.

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

> **Estado al 17/08/2026.** Fases 0 a 4 construidas y corriendo en Docker.
>
> **Este documento es la especificación de referencia**, y se mantiene al día con cada cambio de
> comportamiento: de acá se reconstruye el sistema. El `README.md` cuenta cómo desplegarlo y qué
> se probó; cuando los dos hablen de lo mismo, manda el plan. (Hasta el 15/08/2026 la relación era
> la inversa —el plan quedaba como diseño original y el README era lo vivo— y por eso hay
> secciones que envejecieron; §12 y §9 se corrigieron el 17/08.)
>
> **Decisiones que cambiaron respecto de lo escrito debajo de esta sección**, y conviene leer antes
> de retomar. Las que abrieron sección propia están enlazadas:
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
>   edita ni borra salvo que se lo habiliten. Ver §16.
> - **Los proyectos se archivan, no se borran.** Ver §17.
> - **La app de escritorio tiene login propio** y sesión que no se cierra (token de dispositivo);
>   sigue siendo solo chat y notificaciones.
> - **Todo corre en Docker**, incluido el build del frontend. La configuración es editable desde
>   la interfaz por el admin, salvo la cadena de conexión y la clave de firma.
>
> **Lo que falta de este plan:** digest diario y chat del manager (Fase 4), canal Slack (4b),
> conector Git (5), y probar la app de escritorio con interfaz gráfica.

### Fase 0 — Esqueleto (semana 1)
Solución .NET (`Borlaro.Tms.Api`, `.Domain`, `.Infrastructure`, `.Agent`), proyecto Vite+React, Docker Compose (api + postgres + caddy), EF Core con migración inicial, auth JWT con roles, seed con las 5 plantillas de proyecto.

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
`AgentRunner` sobre el SDK de C# con `BetaToolRunner`, las diez herramientas con schema estricto, `CheckInScheduler` respetando timezone y días laborables, contexto por plantilla, UI de chat en React, tabla `AgentAction` con cola de aprobaciones, digest diario, chat del manager.
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

## 9. Multi-organización (construido de punta a punta)

Hasta acá el plan asume **una instancia = una empresa**: el primer admin nace de `Bootstrap__AdminEmail`, no hay registro, y todos los proyectos son de todos. Esta sección lo abre a varias empresas sobre el mismo despliegue, como hacen GitHub, GitLab y Azure DevOps.

### La entidad que faltaba: `Organization`

Lo que agrupa proyectos es una **organización**, no una persona. La distinción no es cosmética: si los proyectos cuelgan de la cuenta de quien se registró, esa persona no se puede ir de la empresa sin que sea un incidente, no hay forma de tener dos dueños, y la facturación y el dominio de email no tienen dónde vivir.

Quien se registra **crea la organización** y queda como su primer `Owner`.

### Tres niveles de autoridad, no dos

| Nivel | Quién es | Alcance |
|---|---|---|
| **Operador de plataforma** | Quien hospeda Borlaro TMS | La lista de organizaciones: altas, suspensión, métricas de uso. **No** el contenido de los proyectos |
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

- `User.Email` y `Project.Key` pasan de únicos globales a únicos **por organización** (`BorlaroTmsDbContext.cs`, líneas 53 y 120). Así dos empresas pueden tener cada una su proyecto `DEV`.
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

Esta lista se escribió **antes** de construir, y quedó en imperativo. Las cuatro etapas 9a–9d están marcadas como construidas arriba, pero acá no hay resultados anotados: tratala como el plan de prueba a correr, no como prueba corrida.

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

### Varios responsables por etapa, y quién de ellos recibe

Un solo responsable por etapa alcanza mientras la etapa la atiende una sola persona. En cuanto son tres editores, obliga a elegir uno y a que los otros dos miren: o el elegido se convierte en cuello de botella, o alguien reparte a mano todos los días, que es justo lo que la etapa venía a evitar.

**La etapa tiene una lista, no un titular.** `StageResponsibles` —`StageId`, `UserId`, `Order`— es quiénes *pueden* recibir trabajo acá. Y `WorkItemStageAssignments` —`WorkItemId`, `StageId`, `AssignedUserId`— es la excepción por tarea: «esta pieza, cuando llegue a Edición, es de Ana». Se escribe antes de que la tarea llegue, que es cuando se sabe.

**Al mover una tarea se resuelve en cuatro pasos, y el primero que contesta manda:**

1. **Asignación específica** para esa tarea en esa etapa (`WorkItemStageAssignments`) → esa persona. Lo decidió alguien, así que le gana a cualquier cálculo.
2. **Lista de responsables** de la etapa → el **de menos carga**, contando sus tareas abiertas (`ClosedAt == null`); empate se rompe por `Order`, que es el orden en que se los cargó. Solo entran los activos: a una cuenta inactiva no se le apila trabajo.
3. **Titular único de la etapa** (`DefaultAssigneeId`) → esa persona, si está activa. Es como se configuraba antes de que existieran las listas, y sigue siendo lo que edita el encabezado de columna del tablero. **Este paso no es transición: es el modo normal de la mayoría de los proyectos**, y sin él un proyecto configurado a la vieja deja de relevar en silencio en cuanto se aplica la migración de las listas.
4. **Etapa sin configurar** → no cambia de manos. La tarea sigue con quien la tenía, que es lo correcto en etapas de tránsito como «Bloqueado».

La diferencia entre **«no hay a quién darle»** (pasos 1–3 con la etapa configurada y nadie activo) y **«no hay que darle a nadie»** (paso 4) es la que decide si esto es un modo de falla o el funcionamiento normal. Se distingue explícitamente, y de ahí sale el aviso de etapa huérfana de más abajo.

Se reparte por **carga actual y no por turno rotativo** a propósito. El round-robin puro reparte parejo el *número de asignaciones* y desparejo el *trabajo*: a quien tiene seis tareas abiertas le toca la séptima igual que a quien no tiene ninguna. Contar lo abierto es la aproximación más barata a «quién puede tomar esto ahora» sin pedirle a nadie que estime nada.

**Queda registrado que lo eligió el sistema.** `WorkItems.AssignedBySystem` distingue un dueño calculado de uno puesto por una persona. Sin esa marca, un reparto automático desafortunado es indistinguible de una decisión de alguien, y nadie sabe si corregirlo o respetarlo.

**Y queda de dónde venía**, en `PreviousAssigneeId` y `PreviousStageId`: es lo que hace posible la devolución. Cuando una etapa rechaza algo, el camino de vuelta no es «asignar a alguien» sino «volvérselo a quien lo mandó», y eso hay que haberlo guardado en el momento del pase.

**Repartir trabajo es un permiso propio, no un rol.** `Users.CanAssignTasks`, además del permiso del proyecto: quien administra un proyecto no necesariamente es quien decide la carga de la gente, y en equipos con un coordinador esas dos cosas viven en personas distintas.

**Nace encendido, porque es una restricción y no una concesión.** Al agregarse apagado le quitó de golpe a todo el mundo —administradores incluidos— la capacidad de repartir trabajo que tenían desde siempre, y sin ninguna pantalla donde devolvérsela. Encenderlo para todos no le da a nadie nada nuevo: se comprueba *además* del permiso sobre el proyecto, así que quien no lidera sigue sin poder asignar.

Las listas se administran en `/projects/{key}/stages/{stageId}/responsables` (GET, POST, DELETE), y las tres comprueban las dos cosas: el permiso sobre el proyecto y el `CanAssignTasks` de la persona.

**La regla del aviso: se avisa cuando cambia de manos, no cuando cambia de estado.** Si la etapa destino tiene el mismo responsable que ya tenía la tarea, no se dice nada: la persona acaba de hacerlo y contarle lo que ya sabe es la forma más rápida de que aprenda a ignorar los avisos que sí importan.

**Y un relevo no es un check-in**, así que no reusa la escalera. Un check-in insiste, sube a email y termina avisándole al responsable, porque su falta de respuesta significa algo. Un relevo dice «te llegó esto»; que no lo abras en 45 minutos significa que estás trabajando en otra cosa. Aviso plano, sin peldaños.

**Los avisos se agrupan.** Si a la misma persona le caen cuatro tareas en diez minutos, cuatro globos son ruido y uno es información. El relevo no se manda: se encola, y un barrido junta lo que cayó dentro de una **ventana de 2 minutos** (`HandoffService.Ventana`) en un solo aviso.

**El modo de falla que se cubrió explícitamente.** Si el responsable de una etapa queda inactivo, el trabajo se apilaría en silencio sobre una cuenta muerta. En vez de eso la tarea queda **sin asignar** —visible en el tablero— y se les avisa a los administradores. Un ruteo automático que falla callado es peor que el manual.

**Dónde se configura, y por qué ahí.** En el encabezado de cada columna del tablero, no en una pantalla de ajustes aparte. Es donde aparece la pregunta: se mira el tablero, se ve que «Edición» no tiene a nadie, y se resuelve en el acto. Escondido en un formulario de configuración, nadie lo completaría — y un relevo mal configurado es peor que ninguno, porque manda el aviso a la persona equivocada.

Quien no puede repartir trabajo **lo ve pero no lo edita**: enterarse de a quién le va a llegar lo que uno termina no es un privilegio de administración.

✅ **Construido y verificado** (un responsable por etapa): se configuró un responsable en «Redacción», se movió una tarea de otra persona desde fuera del navegador, y cambió de dueño sola. Los avisos se agrupan en la ventana de 2 minutos.

⚠️ **Construido, sin verificar** (varios responsables): las dos tablas, el reparto por menor carga, los tres endpoints y la cadena de cuatro pasos compilan y están migrados, pero **no se probaron contra la base ni de punta a punta**. El reparto por lista, el aviso de huérfana y la caída al titular único están escritos y sin ejercitar.

⏳ **Pendiente:**

**Resuelto:** el encabezado de columna del tablero ahora administra **la lista** —se marca y desmarca gente, y con una sola persona se comporta igual que el titular único—. `CanAssignTasks` tiene su interruptor en la pantalla de Personas junto a los otros dos permisos.

⏳ **Pendiente:** retirar `DefaultAssigneeId`. Ya no se edita desde ningún lado y solo sobrevive como paso 3 para las etapas configuradas antes de que existieran las listas; el desplegable lo dice explícitamente cuando pasa. Migrar cada titular a una lista de un elemento y borrar el campo es la limpieza que queda, y conviene hacerla después de ver el reparto por lista funcionando con datos reales.

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

El nombre del software estaba primero y grande, y la organización al lado en gris chico. Está al revés de cómo se usa: quien abre esto todos los días trabaja para su empresa, y «Borlaro TMS» es apenas un dato de quién proveyó el software.

Ahora la barra abre con el **logo de la organización a 36 px y su nombre a 16 px semibold**; la marca de la plataforma quedó como texto de 11 px, apagado y sin enlace, contra el borde derecho. En la consola del operador sigue mandando «Borlaro TMS»: el operador no pertenece a ninguna empresa, así que no hay logo que poner en su lugar.

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

> **Esto ya pasó una vez, y por eso la lista se mira en cada migración.** `StageResponsibles` y `WorkItemStageAssignments` (§12) entraron con `RESTRICT` hacia `Organizations` y nadie las sumó, así que durante dos días el borrado de cualquier organización con proyectos falló por clave foránea. Ya están en la lista, antes de `WorkItems` y `WorkflowStages`, que es de quienes cuelgan. La regla operativa: **toda migración que agregue una tabla con `OrganizationId` toca también esa lista, en el mismo commit.**

**Los archivos se borran después de que cierre la transacción.** Las rutas se leen antes —después no hay de dónde sacarlas—, pero el borrado en disco va al final: si se borraran adentro y la transacción fallara, los archivos ya no estarían y las filas seguirían apuntándolos. Al revés, un archivo que no se pudo borrar solo ocupa disco.

**Y queda en el log del servidor**, porque en la base no queda nada: borrada la organización se fue también cualquier registro que hubiéramos escrito adentro suyo.

✅ **Construido y verificado**: las tres barreras rechazan como corresponde, una organización real se borró con su archivo, y una consulta de filas huérfanas sobre seis tablas devolvió cero.

⚠️ **La verificación es anterior a las dos tablas de responsables de §12.** Ya están en la lista de borrado, pero la prueba no se volvió a correr desde entonces: hay que repetirla sobre una organización que tenga responsables de etapa cargados.

⏳ **Pendiente:** exportar antes de borrar. Hoy la única forma de conservar algo es el respaldo de la base.

---

## 16. Qué puede hacer el responsable con su propia tarea

**El problema que resuelve.** «Responsable de una tarea» parece un permiso solo, y son tres muy distintos: moverla de etapa, reescribir su enunciado y borrarla. Darlos juntos —que es lo que sale por defecto en casi todos los trackers— hace posible que quien ejecuta el trabajo cambie el trabajo que se le pidió, y en un sistema cuyo diferenciador es que el tablero cuenta la verdad, eso lo vuelve incontable.

**Los permisos son por tarea, no por rol ni por proyecto.** Se deciden al asignarla y los cambia el líder desde el panel de la tarea. Por rol serían demasiado gruesos: la misma persona puede ser dueña del enunciado de una tarea que ella misma levantó y mera ejecutora de otra que le bajaron.

`WorkItems.AssigneeCanMove` (por defecto **`true`**), `AssigneeCanEdit` y `AssigneeCanDelete` (por defecto **`false`**).

**Los valores por defecto no son neutros, y ahí está toda la decisión:**

| Permiso | Por defecto | Por qué |
|---|---|---|
| Mover de etapa | **sí** | Es el trabajo diario de quien ejecuta, y es lo que alimenta al agente. Sin esto, cada avance necesita a un tercero y el tablero se atrasa respecto de la realidad — que es exactamente lo que este producto viene a evitar. |
| Editar título, fechas y campos | **no** | Editar el enunciado es cambiar el encargo, y el encargo es de quien lo dio. Un responsable que puede reescribir su tarea puede hacer que siempre parezca cumplida. |
| Borrar la tarea | **no** | Se lleva el historial, que es el registro de lo que pasó. |

**Nadie puede ampliarse los permisos a sí mismo.** Sin ese corte, quien tiene permiso de editar se da permiso de borrar y el esquema entero es decorativo. Lo comprueba el endpoint aparte de comprobar quién es el líder.

**Cada cambio de permiso queda en el historial** como `permiso:mover`, `permiso:editar` o `permiso:borrar`. Un permiso que cambia sin dejar rastro es indistinguible de uno que siempre estuvo así.

✅ **Construido y verificado** con sesiones reales: por defecto el responsable mueve (permitido), edita (403) y borra (403); al quitarle mover y darle editar, se invierte; no puede ampliarse los permisos a sí mismo (403); y cuando el líder le habilita borrar, borra.

---

## 17. Archivar un proyecto

**El problema que resuelve.** Un proyecto que terminó no se borra —su historial es el registro de lo que hizo el equipo, y es lo que se consulta cuando alguien pregunta cómo se resolvió algo el año pasado— pero tampoco puede seguir ocupando lugar en la lista de todos los días. Sin archivado, la única salida es borrar, y entonces la gente no borra: acumula proyectos muertos hasta que la lista deja de servir.

**Archivar tiene que significar algo más que ocultar.** Si un proyecto archivado sigue admitiendo trabajo nuevo, el archivado es una etiqueta cosmética y en algún momento alguien carga una tarea en un proyecto que nadie mira. Por eso `WorkItemService` rechaza crear trabajo sobre un proyecto archivado, con un mensaje que dice cómo salir del paso: reactivalo. Lo que ya existe adentro sigue siendo consultable y se puede seguir moviendo — cerrar lo que quedó abierto es parte de terminar.

`Projects.IsArchived`, más `ArchivedAt` y `ArchivedById`. Los dos últimos no son adorno: sin ellos, «este proyecto está archivado» no se le puede reclamar a nadie.

**Se deshace.** Es el mismo endpoint con `?undo=true`, y lo limpia todo —marca, fecha y autor—. Archivar por error tiene que costar un clic deshacerlo; si costara un ticket, nadie archivaría.

**Lo hace quien lidera el proyecto o un administrador** (`CanManageProject`), y la lista devuelve `SoyLider` para ofrecer el botón solo a quien puede usarlo. Los archivados salen de la lista por defecto y se ven con el filtro `archivados`; dentro de la lista ordenan al final, porque un proyecto terminado no compite por la atención con uno en curso.

✅ **Construido**: entidad, endpoint con deshacer, filtro en la lista, y el rechazo de trabajo nuevo en el servicio de dominio.

⏳ **Pendiente:** que el agente ignore los proyectos archivados. Hoy el rechazo vive solo en la creación de work items; una tarea que quedó abierta en un proyecto archivado sigue siendo trabajo asignado a los ojos del check-in, así que el agente puede preguntar por ella. Cerrar el proyecto debería ser también dejar de perseguir a la gente por lo que quedó adentro.

---

## 18. El nombre: de TaskAdmin a Borlaro TMS

`TaskAdmin` era provisorio y quedó reemplazado por **Borlaro TMS**. Se registra acá porque el cambio no fue parejo, y quien lea el código va a encontrarse con el nombre viejo en lugares donde sigue estando **a propósito**.

**Lo que sí cambió:** la marca visible (interfaz en los tres idiomas, emails, README, este plan, el manual), y los identificadores de código. Los cinco proyectos pasaron a `Borlaro.Tms.*` con sus namespaces, la solución a `Borlaro.Tms.sln` y el contexto a `BorlaroTmsDbContext`. El binario de escritorio es `BorlaroTms.exe`, **sin espacio**: su ruta se escribe sin comillas en la clave `Run` del registro para el autoarranque, y un espacio ahí la parte en dos y la app no levanta.

**Lo que deliberadamente no cambió, y por qué.** Todo lo que nombra algo que ya existe en una máquina que sirve la aplicación:

| Sigue diciendo `taskadmin` | Qué pasaría si se renombrara |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, la cadena de conexión | Postgres no renombra una base al arrancar: el contenedor levantaría una vacía y los datos quedarían en la vieja, invisibles. Requiere dump y restore a mano |
| `container_name` de los tres servicios | Docker crearía contenedores nuevos al lado de los que están corriendo |
| `TASKADMIN_DOMAIN` | Está en el `.env` de cada instalación; renombrarla las rompe hasta que alguien edite ese archivo |
| `C:\Respaldos\TaskAdmin` y la tarea programada del respaldo | Arrancaría una serie de respaldos nueva dejando la vieja sin rotar, y una segunda tarea al lado de la que ya corre |

**Y hay un tercer lugar que no es ni código ni estado: el SQL escrito a mano.** Los scripts de `deploy/` insertan filas con SQL directo, y los enums se guardan como texto. Cuando los peldaños de la escalera pasaron de `FirstDesktopToast` a `FirstDirectPing` (§20), `disparar-checkin.ps1` siguió insertando el nombre viejo: compilaba todo, el typecheck pasaba, y la API respondía **500 al abrir el check-in** que el propio script acababa de crear. No lo protege ningún compilador, así que **todo renombre de enum tiene que pasar también por `deploy/*.ps1`**.

La regla de fondo: **renombrar es gratis en el código y caro en el estado.** Lo que solo existe en el repositorio se renombra; lo que además existe en un disco ajeno se migra a mano o no se toca. Estos nombres se pueden migrar más adelante, de a uno y con la instalación parada; ninguno es visible para quien usa la aplicación.

✅ **Construido y verificado**: la solución compila con 0 errores tras mover los cinco proyectos, y el frontend construye. El `docker-compose.yml` y el `Dockerfile` ya apuntan a las rutas nuevas.

⏳ **Pendiente:** levantar el compose y confirmar que la imagen se construye de punta a punta con las rutas nuevas — el build de .NET y el de Vite se probaron sueltos, no dentro de Docker.

---

## 19. Tiempo, dificultad, y el líder enterándose

**El problema que resuelve.** Una tarea decía cuándo vencía y nada más. No decía cuánto se pensaba que iba a costar, ni qué tan difícil era, ni —sobre todo— **cuánto terminó costando de verdad**. Sin eso, «la tarea se atrasó» es todo lo que se puede saber, y no alcanza para nada: no distingue la tarea que se subestimó de la que se frenó, ni la persona sobrecargada de la que se trabó en algo puntual. Y el agente, que es lo que este producto tiene de distinto, conversaba a ciegas: podía registrar avance pero no podía preguntar lo único que hace falta preguntar cuando algo no está listo, que es **cuánto más falta**.

Del otro lado, quien lidera un proyecto se enteraba de todo tarde y por casualidad. El agente hablaba con cinco personas, cambiaba tareas de etapa y registraba bloqueos, y nada de eso llegaba a quien tenía que saberlo salvo que fuera a mirar el tablero.

### La estimación: una original que no se toca, y ampliaciones que se apilan

`WorkItems.Estimate` pasa a significar **horas estimadas originales** —antes decía «horas o puntos», que no era ni una cosa ni la otra— y no se sobrescribe nunca después de la primera vez. Cada vez que hace falta más tiempo se agrega una fila en `WorkItemTimeExtensions`: `Hours`, `Reason`, quién la agregó (`ActorType` + `ActorId`, así que el agente queda distinguido de una persona), el `CheckInId` que la originó si vino de una conversación, y `CreatedAt`.

**Por qué una tabla y no un número que se edita.** Un solo campo que se pisa contesta «cuánto falta» y borra «cuánto nos equivocamos». Y esa segunda pregunta es la que sirve: una tarea de 4 horas que terminó en 20 no es un dato sobre esa tarea, es un dato sobre cómo estima ese equipo. Con el campo editable, esa historia se pierde en el momento exacto en que se vuelve interesante.

`WorkItems.AddedHours` guarda la suma, denormalizada. No es la fuente de verdad —esa es la tabla— pero el tablero y los listados necesitan mostrar el total sin sumar filas en cada consulta. **El total comprometido es `Estimate + AddedHours`.**

**No se descartó el triple de Azure DevOps por simplicidad, sino porque el dato que pide se pudre.** *Remaining Work* obliga a que alguien mantenga «cuánto falta» al día en cada check-in, y es justo el campo que en todos los equipos queda con el valor del primer día. Preguntar «¿cuántas horas más?» cuando algo no terminó es una pregunta que se puede contestar de memoria; «¿cuánto te queda en total?» no.

### La dificultad: tres niveles, y para qué sirven

`WorkItems.Difficulty` —`Baja`, `Media`, `Alta`—. Tres niveles y no puntos Fibonacci porque el producto sirve a varias disciplinas y los puntos son jerga de una sola: pedirle story points a quien edita video es pedirle que traduzca su oficio al de otro.

**Su función no es reportar, es modular al agente.** Una tarea Alta que se atrasa es lo esperable, y ahí preguntar cuánto más falta es una conversación normal. Una que se atrasa estando en un nivel bajo significa que pasó algo que nadie previó. Sin dificultad, el agente trata igual los dos casos y se equivoca en los dos.

**Es obligatoria y no tiene valor por defecto.** Ninguna tarea se crea sin que alguien elija el nivel: el formulario arranca vacío, el `select` es `required` y el endpoint devuelve 400 si falta. Un campo del que cuelga una conducta no puede ser opcional —quedaría vacío en la mayoría de las tareas y la conducta no se activaría nunca— pero tampoco puede tener default, que es la trampa sutil: con un valor de fábrica, `Baja` pasaría a significar a la vez «es fácil» y «nadie lo eligió», y el agente estaría modulando sobre un dato que la mitad de las veces no dijo nadie. **Cuesta un clic y salva el dato.**

**El nivel puede cambiar.** Una tarea que resultó más difícil de lo que parecía se reclasifica desde el panel de detalle, y el cambio queda en el historial como cualquier otro. El agente además avisa en su resumen cuando lo que le contaron no se parece al nivel que la tarea tiene.

**Las dos excepciones, y por qué lo son.** Hay dos caminos donde no hay nadie del equipo que pueda juzgar:

- **El formulario público de intake.** Quien crea es alguien de afuera de la organización, y preguntarle a un cliente qué tan difícil le resulta a este equipo su propio pedido no tiene sentido. Entra en `Media`. No es un problema práctico: un pedido de intake entra sin responsable y el agente solo habla de trabajo asignado, así que para cuando lo vea, alguien lo tomó y pudo corregirlo.
- **La tarea que propone el agente** con `create_followup_task`. Acá sí hay quien opine: **el nivel lo propone el agente**, que acaba de conversar sobre ese trabajo, y es un parámetro obligatorio de la herramienta. Quien aprueba lo ve antes de decidir. Si el modelo manda cualquier cosa, cae en `Media`.

En los dos casos el valor de reserva es `Media` y no `Baja`: es el punto medio y no arrastra al agente hacia ninguno de los extremos. Por lo mismo, la migración pone en `Media` las tareas anteriores al campo — ponerlas en `Baja` afirmaría que alguien las consideró fáciles.

**Cada proyecto les pone el nombre que quiera, pero la escala no se toca.** `Project.DifficultyLabelLow/Medium/High`, nulos por defecto; se cargan al crear el proyecto y se editan después en `PUT /api/projects/{key}/dificultad`. Es el mismo mecanismo que ya existía para `ItemNounSingular` y los tipos de tarea: el vocabulario es del proyecto.

Lo que **no** se abre es la escala —ni más niveles, ni menos, ni sin orden—, y no por simplicidad sino por tres razones concretas:

- **El agente usa la posición, no la palabra.** Necesita saber cuál extremo es cuál. Con tres posiciones fijas lo sabe siempre; con una escala libre tendría que inferirlo, y lo inferiría mal alguna vez. Por eso el contexto le llega como «dificultad Compleja (Alta)»: la etiqueta para hablarle a la persona, el nivel para decidir.
- **Se lo estaríamos pidiendo al modelo con menos margen.** §11 documenta que el Q3 gana sobre el Q4 justamente porque duda. Con ocho niveles, «se atrasó una de nivel 3» no tiene respuesta obvia: el modelo se inventa el umbral, y dos modelos —o el mismo en dos días— se inventan umbrales distintos.
- **Se perdería el eje común entre proyectos.** El digest y el chat del manager (§4) tienen que contestar «¿qué está en riesgo esta semana?» sobre toda la organización. Con una escala por proyecto, esa pregunta no tiene respuesta.

Un equipo que necesite una taxonomía propia de verdad ya tiene los **campos personalizados de tipo `Select`**, que existen para eso: el agente los ve en el contexto y hasta puede escribirlos, pero no razona sobre ellos —correcto, porque nadie le explicó qué significan—. Esa es la línea: `Difficulty` es el campo con semántica que el agente entiende, y los campos personalizados son todo lo demás. Abrir la escala borraría la diferencia y dejaría dos mecanismos haciendo lo mismo mal.

**La estimación original se congela cuando la tarea arranca** —cuando tiene avance o alguna ampliación—. Antes de eso se corrige libremente: un número mal tipeado el primer día es un error y prohibir arreglarlo obligaría a rehacer la tarea. Después, no: bajarla cuando ya se agregaron doce horas hace que el trabajo entre en lo estimado retroactivamente, y borra el único dato que esta sección existe para conservar. El camino correcto para decir que hace falta más tiempo es agregarlo, no reescribir el pasado.

**Una ampliación se puede anular, y anular no es borrar.** `VoidedAt`, `VoidedById` y `VoidReason`; una anulada no cuenta para el total pero se sigue viendo, tachada. Hace falta porque **estas filas las escribe el agente solo**: si el modelo entiende mal, o la persona tira «como seis» y resulta que era una, sin esto quedan seis horas registradas para siempre. Y se conserva porque «acá hubo una ampliación que resultó estar mal» es información —es cómo alguien se entera de que el agente se equivocó—; borrarla deja el historial contando una versión prolija de algo que no pasó así. **Anular es de quien lidera**, no de quien tiene la tarea: si pudiera anularlas el responsable, controlaría el registro de cuánto costó su propio trabajo.

**`AddedHours` se recalcula entero desde la tabla, nunca se incrementa.** Sumar de a poco funciona mientras haya un solo camino de escritura y deja de funcionar apenas aparece el segundo —anular—, porque ahí hay dos lugares que pueden equivocarse y ninguno se entera del otro.

### El agente pregunta por las horas, y no pide permiso para registrarlas

Herramienta nueva `add_time_estimate` (`work_item_id`, `hours`, `reason`). Cuando la persona dice que no terminó, el agente pregunta cuántas horas más calcula, y registra la respuesta.

**Se aplica en el acto, a diferencia de `request_date_change`, que sigue yendo a la cola de aprobación.** La distinción es deliberada y vale la pena entenderla: mover una fecha de entrega **cambia un compromiso con un tercero**, y eso no lo puede decidir la persona que se atrasó. Agregar horas **no cambia ningún compromiso: registra lo que ya está pasando.** Mandarlo a una cola tendría el efecto de que el dato llegue tarde o no llegue, y un registro de tiempo que depende de que alguien lo apruebe deja de ser un registro.

Lo que sí es innegociable: **cada ampliación avisa**, al líder del proyecto y a los administradores. Es el único punto donde el sistema dice «esto está costando más de lo que se dijo» mientras todavía se puede hacer algo.

### La fecha de entrega: se espera al arrancar, no se exige nunca

**El problema.** Una tarea en el backlog dentro de tres meses no tiene compromiso con nadie y ponerle fecha es inventar un dato. Pero una que alguien ya está haciendo sin fecha es trabajo que nadie sabe cuándo llega — y sin `DueDate` se apagan tres cosas a la vez: el aviso `item.overdue`, el orden de «mis tareas», y una de las cuatro señales del modo «cuando hace falta».

**La decisión: nunca bloquear, siempre avisar.** Entrar a una etapa `InProgress` sin fecha **se permite**. Bloquear el movimiento dejaría a alguien sin poder trabajar por un campo de planificación que no le toca completar, y castigaría a quien ejecuta por la omisión de quien lidera. En vez de eso: la tarjeta lo muestra en el tablero, y quien lidera recibe la novedad `item.started_without_date`.

Es el mismo patrón que el resto del sistema ya usa —la etapa huérfana de §12 deja la tarea sin asignar y avisa, el cambio de fecha de §19 se propone en vez de aplicarse— y bloquear acá habría sido la única excepción.

**Fijar una fecha es un permiso propio**, `User.CanSetDueDate`, porque una fecha es un compromiso con un tercero: o se confía en que esa persona los asuma, o no, y eso no cambia según la tarjeta. Quien no lo tiene sigue moviendo tareas y registrando avance; lo único que no puede es prometer una entrega. Es también lo que resuelve el reemplazo por vacaciones sin regalar el proyecto entero — aunque para eso alcanza además con **agregar un segundo líder**, que los líderes ya son una lista.

**Crear tareas también es un permiso propio**, `User.CanCreateTasks`, para el caso inverso: quitárselo a quien solo debe ejecutar lo que le dan.

Los dos, como `CanAssignTasks`, **nacen encendidos**: son restricciones que alguien aplica a mano, no permisos que haya que conceder de a uno. Los tres se editan en la pantalla de Personas, que es donde se los busca.

### El agente y las etapas

El contexto le llegaba con el **nombre** de la etapa y no con su categoría, así que el modelo no tenía forma de saber si «Redacción» era trabajo en curso o una fila de espera. Ahora recibe `etapa Redacción (InProgress)`, y con eso se habilita la conversación que faltaba:

**Cuarta señal para el modo «cuando hace falta».** A las tres que había —vencida, estancada, bloqueada— se suma **«no tiene nada en curso pero sí cosas por hacer»**. No hay nada roto todavía; lo que hay es una persona a punto de elegir en qué gasta el día, y ese es el único momento en que preguntar cambia algo. El agente le ofrece las de `Todo` y le pregunta cuál toma. Si tampoco tiene nada por hacer, no escribe.

### El panel de novedades del líder

Página propia (`/novedades`) con campana y contador de no leídas en el encabezado. Se alimenta de la tabla `Notifications`, que ya existía para la escalera de entrega y hasta ahora no tenía dónde leerse dentro de la aplicación.

Cinco clases de novedad, que son las cinco cosas que quien lidera necesita saber sin ir a buscarlas:

| `Kind` | Cuándo |
|---|---|
| `item.overdue` | Una tarea pasó su fecha de entrega sin cerrarse |
| `checkin.opened` | El agente le escribió a alguien del proyecto |
| `checkin.answered` | La persona respondió, **con el resumen de qué dijo** |
| `item.time_extended` | Se agregaron horas, con cuántas y por qué |
| `item.stage_changed_by_agent` | El agente movió una tarea de etapa |
| `item.started_without_date` | Una tarea entró en curso sin fecha de entrega |

**Se avisa al líder del proyecto y a los administradores, no a todo el mundo.** Una novedad que le llega a diez personas no la atiende ninguna: cada una supone que la mira otra. Y `checkin.answered` lleva el resumen de la respuesta y no solo «respondió»: un aviso que obliga a abrir otra pantalla para saber qué pasó es un aviso que se ignora a la tercera vez.

**Los check-ins no cuelgan de un proyecto**, así que sus destinatarios se resuelven por persona: quien lidera algún proyecto donde esa persona tiene trabajo abierto. Es exactamente quien necesita saber que respondió.

**`item.overdue` la detecta un barrido y se avisa una sola vez.** Vencer no es un hecho que alguien dispare —es que pase el tiempo—, y sin la marca `WorkItems.OverdueNotifiedAt` cada pasada del barrido volvería a contar lo mismo y el feed quedaría inservible en una tarde. La marca se limpia al mover la fecha: reprogramar y volver a vencer sí es una novedad. El barrido toma 50 por pasada, para que una instalación que arranca con mil vencidas no genere mil avisos por destinatario de una vez.

**Las novedades leídas se podan a los 30 días.** Cada una escribe una fila **por destinatario**, así que un proyecto con tres líderes y dos administradores multiplica por cinco cada aviso, y hasta acá lo único que vaciaba la tabla era borrar la organización. Solo las leídas: que nadie haya mirado una en un mes es un problema, y desaparecerla lo esconde en vez de resolverlo.

**Es un feed, no una bandeja de entrada.** No se contesta desde ahí; cada novedad enlaza a la tarea o al check-in. Mezclarlo con los mensajes directos —que fue la alternativa— habría hecho que lo automático tape lo humano, que es el modo en que estas dos cosas siempre conviven mal.

⚠️ **Construido entero y desplegado; falta el uso real.**

✅ Verificado contra la base: las seis migraciones aplicadas (`MultiplesResponsables`, `PermisoAsignarPorDefecto`, `TiempoDificultadYNovedades`, `AnulacionDeAmpliacionYAvisoDeVencimiento`, `DificultadObligatoriaYEtiquetas`, `PermisosDeFechaYCreacion`), `Difficulty` quedó NOT NULL con las filas anteriores en `Media`, las tres tablas nuevas creadas, las tres etiquetas en `Projects`, y los tres permisos encendidos en las 9 cuentas existentes. La API arranca sana (`/health` 200, sin errores en el log), el frontend construye y sirve sin errores de consola, y las rutas nuevas responden 401 —existen y exigen sesión— contra el 404 de una ruta inventada.

⚠️ **Lo que no está verificado es el comportamiento con sesión**: ninguna de las pantallas nuevas se ejercitó con datos reales, y el agente no conversó ni una vez con las herramientas nuevas.

Lo que quedó escrito:

- El modelo: `AddedHours`, `Difficulty`, `OverdueNotifiedAt`, la tabla `WorkItemTimeExtensions` con su anulación, y el recálculo de `AddedHours` desde la tabla. La tabla nueva entró en la lista de borrado de §15.
- La herramienta `add_time_estimate` —las del agente pasaron de nueve a diez—, las instrucciones para preguntar por las horas sin inventarlas y modular según la dificultad, y el contexto de tareas con el tiempo como «original + agregadas» y la dificultad como «etiqueta del proyecto (nivel real)».
- La dificultad obligatoria en los tres caminos de alta, editable después, y renombrable por proyecto desde el alta o con `PUT /api/projects/{key}/dificultad`.
- `CanSetDueDate` y `CanCreateTasks`, con sus cortes en la API, en la sesión y con sus interruptores en Personas — donde además apareció que `CanAssignTasks` llevaba desde su migración sin ninguna pantalla.
- La fecha de entrega y la estimación **editables desde el panel de detalle**, que era el agujero concreto: la API las aceptaba desde siempre y ninguna pantalla las mostraba.
- El congelamiento de la estimación original y el endpoint de anulación, restringido a quien lidera.
- `FeedService` completo con las cinco novedades emitiendo, el barrido de vencidas y la poda de leídas, los dos enganchados al barrido de check-ins que ya existía.
- La API del feed y la de ampliaciones.
- La interfaz: fecha, horas y dificultad en el formulario de creación; el bloque de tiempo con las ampliaciones y su anulación en el panel de detalle; `/novedades` con la campana y el contador en el encabezado. Todo en los tres idiomas.

⏳ **Pendiente — todo requiere una sesión y datos reales:**

- Que el agente pregunte por las horas y las registre; que una ampliación se pueda anular y el total vuelva atrás; que la estimación congelada rechace el cambio; que las seis novedades lleguen a quien corresponde y a nadie más.
- Que la dificultad obligatoria no trabe el alta rápida, y que el reparto por lista elija efectivamente al de menos carga.
- **Repetir la prueba de borrado de organización de §15**, ahora que hay una tabla más en la lista.
- **Confirmar que la dificultad obligatoria no molesta en el uso diario.** Es un clic más en cada alta, y el formulario rápido existe para anotar algo en dos segundos. Si en la práctica frena, la salida no es ponerle un default —eso rompe el dato— sino recordar el último nivel elegido en ese proyecto.

---

## 20. Un canal por persona, y por qué no tres apps de escritorio

**El problema.** La app de bandeja es WPF, o sea Windows. Pero los equipos no son homogéneos: el proyecto de video trabaja en Mac, el de desarrollo mezcla Windows y Linux y suma Mac cuando hay algo para iOS. Para media empresa, el primer peldaño de la escalera no existía — y la escalera lo descubría recién a los 45 minutos, cayendo a correo.

**La salida obvia era construir la app para Mac y Linux. Se descartó, y no por costo.**

Lo que hace la app de escritorio es: recibir un push, mostrar un aviso, abrir el chat y acusar recibo. **Slack hace exactamente eso, en los tres sistemas y además en el teléfono**, sin empaquetar, firmar, notarizar en Apple, armar `.deb`/`.rpm`/AppImage ni mantener tres mecanismos de autoactualización. Construir tres clientes nativos sería resolver por triplicado un problema que un canal ya resuelto resuelve mejor.

Y **la señal que devuelve es más fuerte, no más débil**. El argumento para poner el escritorio primero era su acuse confiable de apertura. Pero el acuse dice «abrió el aviso»; una respuesta en Slack dice «contestó, y esto dijo» — que es el dato que alimenta al agente y el que realmente importa.

Si alguna vez hace falta un cliente nativo —una empresa que no use Slack ni Teams—, se hace **uno solo multiplataforma con Avalonia**, que es .NET y reutiliza `AgentConnection` y `AgentApi` sobre SignalR casi tal cual. Electron o Tauri obligarían a reescribir esa capa en otro lenguaje para no ganar nada. Tres, nunca.

**El canal es de la persona, no del proyecto.** Aunque los proyectos tiendan a agrupar sistemas operativos, la misma persona está en varios proyectos y sigue teniendo una sola computadora. Por eso `PreferredChannel` vive en `Users` y no en `Projects`.

**Qué falta para Slack, en orden:**

1. `SlackChannel : INotificationChannel` — entrega por mensaje directo del bot. Es la parte fácil: la escalera ya no necesita enterarse.
2. **La vuelta**, que es la difícil: endpoint público para Events API, verificación de la firma de Slack, y mapear «este usuario de Slack es esta persona». Nada de eso se comparte entre proveedores.
3. Vincular la cuenta: una persona tiene que poder decir «este soy yo en Slack», y hasta que no lo haga su canal Slack no puede entregar.
4. Teams después, reusando 1 y 3 con su propio proveedor.

**Dos requisitos que no son de código:** las credenciales de la app de Slack —bot token y signing secret, que van cifradas en Configuración— y una **URL pública y estable** para los webhooks, que hoy la instalación no tiene porque vive detrás de Caddy en una red doméstica.

✅ **Construido y verificado**: la escalera resuelve el canal por persona; `Users.PreferredChannel` con su selector en Personas, y el correo rechazado como preferido. Migración aplicada sobre la base real, incluido el renombre de los peldaños ya guardados —dos filas de `FirstDesktopToast` pasaron a `FirstDirectPing`— sin romper el historial de entregas. La API arranca sin errores.

⏳ **Pendiente:** todo Slack, del punto 1 al 4. Y probar la escalera nueva con dos personas de canales distintos, que es lo único que demuestra que el reparto por canal funciona.

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
