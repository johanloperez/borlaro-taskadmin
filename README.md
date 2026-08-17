# Borlaro TMS

Gestión de trabajo para equipos de cualquier disciplina —desarrollo, diseño, edición de video,
contenido, operaciones— con un **agente de IA que conversa cada día con cada persona**, le
pregunta el estado de lo asignado y traduce la respuesta en cambios concretos del tablero.

El plan completo —investigación de mercado, arquitectura, modelo de datos, fases y criterios de
verificación— está en [docs/PLAN.md](docs/PLAN.md).

**Para quien usa la plataforma**, el manual se sirve desde la propia instalación en **`/manual`**,
y hay un enlace en el menú de la aplicación. El archivo es
[web/public/manual.html](web/public/manual.html) — una sola copia, servida y versionada en el
mismo lugar. Cubre el uso diario, el agente de check-ins, el relevo por etapa, la administración y
la configuración. Este README y el plan son para quien la despliega y la programa; el manual no
supone nada de eso.

## Estado

**Fases 0 (esqueleto), 1 (tracker), 2 (entregables y revisión) y 3 (canales y entrega
garantizada) completas y verificadas. Fase 4 (el agente) construida y verificada contra un
modelo guionado; falta probarla contra un modelo real y faltan el digest y el chat del
manager.**

**Multiempresa: construido de punta a punta.** Todo cuelga de una `Organization` y ninguna ve nada
de otra. Hay tres niveles de autoridad —operador de la instalación, administrador de cada empresa,
y los roles de siempre adentro—, consola para dar de alta y suspender organizaciones, alta con
Google en un paso, y configuración por empresa que hereda de la plataforma. Se apaga entero con
`TENANCY_MODE=Single`, que es el modo on-premise. Ver [Instalaciones multiempresa](#instalaciones-multiempresa)
y [§9 del plan](docs/PLAN.md).

| Pieza | Estado |
|---|---|
| Solución .NET 8 con cuatro proyectos | ✅ |
| Modelo de dominio y DbContext (EF Core + Npgsql) | ✅ |
| Migración inicial — 25 tablas aplicadas | ✅ probado |
| Seed de las 5 plantillas de disciplina | ✅ probado |
| Auth JWT con los 4 roles + bootstrap del primer admin | ✅ probado |
| Docker Compose con Postgres 16 | ✅ probado |
| Creación de proyectos desde plantilla | ✅ probado |
| Motor de workflow con transiciones válidas | ✅ probado |
| Campos personalizados con validación por tipo | ✅ probado |
| CRUD de work items con historial por campo | ✅ probado |
| Tablero Kanban con drag & drop | ⚠️ ver abajo |
| Panel de detalle con campos dinámicos e historial | ✅ probado en navegador |
| Almacenamiento de archivos (`IFileStore`) | ✅ probado |
| Entregables con versiones (archivo y enlace) | ✅ probado |
| Rondas de revisión: aprobar / pedir cambios | ✅ probado |
| Formulario público de intake con límite de tasa | ✅ probado |
| Hub SignalR con heartbeat y doble acuse | ✅ probado |
| Escalera de entrega (5 peldaños, canal de cada persona → Email → líder) | ⚠️ desplegado; sin probar con dos canales — [§20](docs/PLAN.md) |
| Programador de check-ins por zona horaria | ✅ probado |
| App de escritorio WPF en bandeja | ⚠️ conecta y late; la interfaz no está probada |
| ABM de personas con roles y horario de check-in | ✅ probado |
| Miembros de proyecto (al crear y después) | ✅ probado en navegador |
| Sección de Configuración para el admin | ✅ probado, aplica en caliente |
| Mensajes directos con entrega por escritorio/email | ✅ probado |
| Las 10 herramientas del agente contra el dominio | ✅ probado (9; `add_time_estimate` sin probar) |
| Bucle de conversación con tope de turnos y transcript | ✅ probado |
| Cola de aprobaciones (fecha y trabajo nuevo) | ✅ probado en navegador |
| UI de chat en React (`/agent-chat`, `/checkin/:id`) | ✅ probado en navegador |
| Conversación contra un modelo local (Ollama, `qwen3-taskadmin`) | ✅ probado de punta a punta |
| Aviso al escritorio por SignalR, con acuse de la app | ✅ probado de punta a punta |
| Tablero en tiempo real (sin refrescar) | ✅ probado en navegador |
| Relevo: responsable por etapa y aviso al recibir | ✅ probado de punta a punta |
| Varios responsables por etapa, con reparto por menor carga | ⚠️ desplegado con interfaz; sin probar en uso — [§12](docs/PLAN.md) |
| Archivar proyectos, con deshacer | ✅ construido — [§17](docs/PLAN.md) |
| Estimación original + ampliaciones anulables, dificultad, `add_time_estimate` | ⚠️ desplegado y migrado; sin probar en uso — [§19](docs/PLAN.md) |
| Novedades del líder: `/novedades`, campana y las 6 clases de aviso | ⚠️ desplegado; sin probar en uso — [§19](docs/PLAN.md) |
| Permisos por persona: asignar, fijar fechas, crear tareas | ⚠️ desplegado; sin probar en uso — [§19](docs/PLAN.md) |
| Nombre y logo de la organización en la sesión | ✅ |
| Manual para quien usa la plataforma, servido en `/manual` | ✅ probado |
| Conversación contra Claude (`ANTHROPIC_API_KEY`) | ⚠️ sin clave, sin probar |
| Digest diario y chat del manager | ⛔ no construidos |

Verificado end-to-end: dos proyectos de disciplinas distintas conviven con sus propias etapas,
campos y vocabulario; las transiciones inválidas se rechazan con 409 tanto por API como desde la
UI; una etapa de bloqueo exige motivo y al salir cierra el bloqueo; los cuatro casos de
validación de campos personalizados fallan como corresponde; y cada cambio deja su evento.

El ciclo completo de revisión está probado de punta a punta: el diseñador sube v1, el cliente
pide cambios con comentarios, se sube v2 sobre el mismo entregable, el cliente aprueba, y queda
registrado que llevó **2 rondas** con v2 como versión aprobada. Rechaza además: abrir dos
revisiones sobre la misma versión, que un tercero resuelva una ronda ajena, y pedir cambios sin
decir cuáles.

El formulario público de intake es la única superficie anónima con escritura del sistema, así
que está acotado: apagado por defecto, protegido por un token de 24 bytes aleatorios (no por la
clave del proyecto, que es adivinable), con límite de 10 pedidos por minuto por IP, topes de
longitud validados en el servidor, y una respuesta que solo devuelve el número de referencia —
nada del estado interno del proyecto. Apagarlo borra el token, así que los enlaces ya repartidos
dejan de funcionar y reactivarlo emite uno nuevo.

La escalera de entrega está probada en sus dos escenarios, que es lo que importa: **sin
heartbeat vivo** salta los toasts y cae directo a email; **con la app corriendo** entrega por
Desktop (`DeliveredVia = Desktop`, acusado por la app vía SignalR) y solo después escala a
email. En ambos casos, si nadie lo abre termina en `Missed` con aviso al manager — y **abrir el
check-in detiene la escalera en seco**: verificado que tras 20 segundos (siete barridos) el
peldaño y el número de intentos no se mueven.

Para probar la escalera comprimida en segundos en vez de horas:

```bash
Notifications__SweepIntervalSeconds=3 Notifications__SecondToastAfterMinutes=0 Notifications__FirstEmailAfterMinutes=0 Notifications__SecondEmailAfterMinutes=0 Notifications__MarkMissedAfterMinutes=0 dotnet run --project src/Borlaro.Tms.Api
```

Sin `Email:SmtpHost` configurado, los correos se escriben como archivos `.eml` en
`src/Borlaro.Tms.Api/outbox/`, así se puede probar la escalera sin montar un SMTP ni mandarle
correo de verdad a nadie.

El agente está probado en el ciclo que importa: el scheduler crea el check-in a la hora local,
abrirlo detiene la escalera, el modelo lee el trabajo asignado con `get_assigned_tasks` y la
conversación se traduce en cambios reales — se abre el bloqueo con el motivo en las palabras de
la persona, se registra el avance, y **el cambio de fecha no se aplica**: queda en la cola hasta
que un responsable lo aprueba, y recién ahí se mueve la fecha. Todo con `ActorType.Agent` y el
`CheckInId` que lo originó. Cerrar y volver a abrir retoma la conversación donde quedó
(`status = Partial`), escribir en un check-in cerrado da 400, y «todo igual que ayer» lo cierra
sin llamar al modelo (0 turnos, 0 tokens).

**El modelo se configura desde la interfaz**, en Configuración → Modelo de IA: proveedor
(`anthropic`, `openai-compatible` —Ollama, LM Studio, Groq— o `scripted`), modelo, clave de API,
URL base, tope de turnos, tope de tokens y esfuerzo. Se aplica en caliente: el proveedor se
resuelve en cada conversación, así que cambiarlo no requiere reiniciar nada.

**Sin clave de API el agente cae al modelo guionado** en vez de fallar en el primer check-in del
día. Eso es deliberado, pero silencioso, así que la pantalla dice **qué está corriendo de
verdad** y no solo qué está configurado:

> ⚠ Ahora mismo responde: **modelo de prueba** — Está configurado Anthropic pero no hay clave de
> API. Cargá la clave acá abajo para que use el modelo de verdad.

### Modelo local (Ollama / LM Studio)

El botón **«Detectar modelos locales»** de Configuración le pregunta al servidor qué tiene
instalado y ofrece los nombres como sugerencias en el campo Modelo, en vez de hacerlos escribir a
mano: un typo en `qwen3-coder:30b-32k` falla recién en el primer check-in del día, con un 404 del
proveedor que no explica nada.

Con la API en Docker y Ollama en la máquina, la URL base tiene que ser
`http://host.docker.internal:11434/v1` — `localhost` ahí adentro es el propio contenedor. El
compose ya declara `host.docker.internal` como `host-gateway` para que funcione también en Linux,
y el error de conexión lo dice explícitamente en vez de devolver un timeout pelado.

**Ojo con el contexto:** los modelos de Ollama sin `num_ctx` definido usan **4096 tokens**, y el
prompt del agente —sistema, diez herramientas y el tablero de la persona— lo supera. Con el
contexto corto el modelo pierde las definiciones de herramientas y deja de llamarlas, sin ningún
error visible. Conviene una variante con contexto declarado (`…-32k`).

**Y ojo con el `PARSER`, que falla más silenciosamente todavía.** Un modelfile propio sin las
directivas `RENDERER`/`PARSER` deja al modelo emitiendo sus llamadas como texto plano —
`<tool_call>{"name": "log_progress"…}</tool_call>` dentro del mensaje— y Ollama nunca las
convierte en `tool_calls`. La API lo ve como charla: el agente parece funcionar, conversa bien, y
el tablero no se actualiza nunca. Si armás un modelfile a mano, copiale las tres líneas al
oficial:

```
TEMPLATE {{ .Prompt }}
RENDERER qwen3-coder
PARSER qwen3-coder
```

#### Qué modelo, medido

En un Ryzen 9 6900HX (8 núcleos, 27,7 GB, **sin GPU discreta**) el modelo corre en CPU. Ahí sirve
una arquitectura **MoE**: **Qwen3-Coder-30B-A3B** pesa 30B pero activa 3B por token, así que rinde
como uno chico y responde como uno grande. **4 a 7 segundos por turno**, unos 40 s la primera vez
por la carga.

Comparando Q3 y Q4 con parámetros idénticos, sobre los casos donde el agente tiene que decidir si
escribir o preguntar (3 vueltas cada uno):

| | Q3_K_XL (13 GB) | Q4_K_M (18 GB) |
|---|---|---|
| No escribió de más | **9/9** | 6/9 |
| Tarea que no existe | **3/3** | **0/3** — le colgó el bloqueo a otra tarea |
| Convive con los contenedores | sí (15 GB cargado) | **no**: ocupa 20 GB y voltea a Docker |

Contra la intuición **gana el más cuantizado**: el Q4 es más decidido y actúa; el Q3 duda y
pregunta, que acá es la conducta correcta. Un dato omitido se corrige solo; un bloqueo falso en el
tablero de otro no.

Verificado de punta a punta contra ese Q3 (`temperature 0.3`, `num_ctx 16384`): con el tablero
vacío el agente contestó «No tenés tareas asignadas», y ante un «lo de ayer quedó a medias»
respondió que no podía saber de qué tarea se trataba y volvió a preguntar — sin inventar nada y
sin escribir en el tablero.

## Administración

**Personas** (`/personas`, solo Admin). Alta con rol, zona horaria, hora del check-in y días
laborables; edición en línea, cambio de contraseña y desactivación. Dos reglas duras: nunca se
borra a nadie —su nombre cuelga de tareas, comentarios y transcripts, y un borrado dejaría el
historial hablando de un fantasma— y la instancia no puede quedarse sin ningún admin activo, ni
por desactivación ni por cambio de rol.

## Idiomas

Español, inglés y portugués. El idioma es **de cada persona**: sale del navegador y se cambia con
el selector del encabezado, también en las pantallas públicas. La elección se guarda en el usuario,
así que viaja entre dispositivos y determina además en qué idioma llegan los emails.

Las traducciones son diccionarios en código: `web/src/locales/{en,es,pt}.ts` para la interfaz y
[Messages.cs](src/Borlaro.Tms.Api/Localization/Messages.cs) para el servidor. El **inglés** es el
idioma de reserva y el diccionario de referencia: los otros dos se tipan contra él, así que **una
traducción que falta no compila**. Agregar un idioma es agregar un archivo.

**Toda la interfaz** está en los tres idiomas —577 claves— junto con los mensajes del servidor de
login, dispositivo y alta, y los correos del registro. Siguen en castellano los **datos semilla**:
los nombres y las etapas de las cinco plantillas de fábrica, que son filas de la base y no cadenas
de la interfaz. Ver [§10 del plan](docs/PLAN.md).

## Instalaciones multiempresa

Por defecto (`TENANCY_MODE=Single`) la instalación es de una sola empresa y funciona como siempre:
sin registro, sin consola de plataforma, con el admin que sale del `.env`. Es el modo on-premise.

Con `TENANCY_MODE=Multi` varias empresas comparten el despliegue sin verse entre sí:

- **La organización es la dueña de todo.** Personas, proyectos, plantillas y conversaciones
  pertenecen a una, y cada tabla lleva su columna. El aislamiento no depende de que cada consulta
  se acuerde de filtrar: lo aplica el DbContext.
- **El operador** (`PLATFORM_OPERATOR_EMAIL`) da de alta organizaciones, las suspende y las
  reactiva desde `/plataforma`. Ve cuánta gente, cuánto trabajo y cuántos tokens consume cada una;
  **no ve el contenido de ninguna**, porque su cuenta vive en su propia organización y el filtro lo
  acota igual que a cualquiera. Suspender no borra nada.
- **Y puede borrarlas del todo**, con tres barreras: hay que suspenderla antes, hay que escribir
  su nombre exacto, y la organización de la plataforma nunca —borrarla dejaría la instalación sin
  quien la administre—. Se van sus personas, proyectos, tareas, conversaciones y archivos, sin
  papelera. El borrado corre en una transacción y las 27 claves foráneas en `RESTRICT` garantizan
  que sea todo o nada. Ver [§15 del plan](docs/PLAN.md).
- **El alta está en `/registro`, por dos caminos.** Con el proveedor de identidad es un clic —
  Google ya verificó la dirección, así que solo falta el nombre de la empresa. Con email y
  contraseña hay una vuelta más: la organización **no** se crea al enviar el formulario sino al
  hacer clic en el enlace del correo, porque un endpoint público que crea empresas sin verificar
  la dirección se llena de organizaciones fantasma. Se apaga entero con
  `TENANCY_ALLOW_REGISTRATION=false` y se acota con `TENANCY_ALLOWED_DOMAINS`.
- **Una cuenta por organización.** Si la misma dirección trabaja para dos empresas, son dos
  cuentas. Todavía no existe el paso de elegir a cuál entrar: cuando pasa, el login lo dice en vez
  de elegir una por el orden de las filas.
- **La configuración tiene dos capas.** La plataforma pone el modelo de IA y el SMTP, y cada
  organización puede reemplazarlos desde su propia pantalla —para apuntar el agente a su Ollama,
  por ejemplo—. Lo que no reemplaza, lo hereda. El login, las sesiones y la URL pública son de la
  instalación y solo los toca el operador.
- **Cada organización se ve en el encabezado**, con su nombre y su logo. El logo se carga al
  crearla —en los tres caminos que la crean, con la misma validación— y se sirve **con sesión**,
  no como archivo público: las empresas no se ven entre sí y su logo tampoco.

  ⚠️ **`Organizations` es la única tabla donde el aislamiento no viene solo.** No implementa
  `IOrganizationScoped` —es la organización, no algo que le pertenezca—, así que el filtro global
  del DbContext no la cubre. Cualquier endpoint nuevo sobre esa tabla tiene que comprobar a mano
  que la organización pedida sea la de quien pregunta, o que sea el operador. Ver
  [§14 del plan](docs/PLAN.md).

## Permisos

Hay **dos niveles de rol** y hacen falta los dos: uno dice qué podés hacer en la instancia, otro
qué podés hacer en un proyecto concreto.

| Rol en la instancia | Qué puede |
|---|---|
| **Operador de plataforma** | Solo en modo multiempresa. Da de alta organizaciones, las suspende y las reactiva. No pertenece a ninguna y no ve el contenido de ninguna. |
| **Admin** | Administra su organización: la configura, gestiona personas y plantillas, y entra a cualquier proyecto de ella sin ser miembro. |
| **Manager** | Líder de equipo: crea proyectos —queda como líder de los que crea— y ve el panorama completo. Sobre un proyecto que no lidera, participa como cualquiera. |
| **Colaborador** | Trabaja donde tiene tareas asignadas. Puede liderar un proyecto sin ser Manager. |
| **Cliente revisor** | Ve y aprueba lo suyo. **No crea ni mueve trabajo**, ni aunque tenga algo asignado. |

### Participar es tener trabajo asignado

**No hay lista de miembros.** Se participa de un proyecto teniendo una tarea asignada ahí, y esa
es toda la regla. Una lista aparte hay que mantenerla sincronizada con la realidad, y se
desincroniza el primer día: alguien queda «en el equipo» sin nada que hacer, o trabajando sin
figurar. Acá el proyecto aparece en tu lista cuando te asignan algo, y desaparece de la de quien
nunca tuvo nada.

Lo único que se designa es **quién lidera**, porque no se puede deducir del tablero y hace falta
antes de que exista la primera tarea. Quien crea el proyecto queda como líder.

**Solo Admin y Manager pueden liderar un proyecto.** El selector ofrece únicamente a esas
personas y el backend rechaza al resto nombrándolas. Y no se le puede bajar el rol a alguien que
lidera: hay que pasarle el liderazgo primero, o esos proyectos quedarían con un responsable que
ya no podría serlo — la regla valdría al designar y no después, que es como se degradan las
reglas hasta dejar de significar algo.

**Cada uno ve solo los proyectos donde participa**, incluidos los Manager. El único que ve todo
es el **Admin**: es el superusuario y la salida de emergencia cuando algo se rompió.

| Dentro de un proyecto | Puede |
|---|---|
| **Líder** (designado) | Mover cualquier tarea, asignar y desasignar, archivar el proyecto, abrir el formulario público, designar otros líderes. |
| **Con trabajo asignado** (deducido) | Ver el tablero completo y **mover solo sus propias tareas**. Crea trabajo para sí mismo o sin asignar. |
| **Revisor** (deducido de las rondas) | Ver y aprobar entregables. No crea ni mueve nada. |
| **Sin nada de eso** | No ve el proyecto: **404**, no 403 — un 403 sobre `/api/projects/NOMINA` ya confirma que ese proyecto existe. |

Verificado con sesiones reales, empezando por alguien sin tareas en el proyecto:

| Paso | Resultado |
|---|---|
| El colaborador no tiene tareas ahí | No ve el proyecto · tablero **404** |
| La líder le asigna una tarea | El proyecto **aparece solo**, sin que nadie lo agregue a ninguna lista |
| Mueve **su** tarea | ✅ |
| Reasigna esa misma tarea a otro | **403** |
| Se desasigna a sí mismo | **403** |
| Mueve una tarea ajena | **403** |
| Archiva el proyecto | **403** (es del líder) |

Las tarjetas que no son tuyas **no se pueden arrastrar** cuando no liderás: se abren para mirar,
pero el arrastre está desactivado. Dejar arrastrar para devolver un 403 al soltar es una forma
cara de enterarse.

El tablero tiene un **panel lateral** con la información del proyecto, quiénes participan, con qué
rol y cuánta carga abierta tiene cada uno, y **cada tarjeta muestra a quién está asignada** —o
«sin asignar», que es una señal en sí misma. El responsable se cambia desde el panel de detalle
de la tarea, y solo lo ve editable quien lidera.

## Actividad y auditoría

`/actividad` está acotada a **los proyectos donde participás**; el admin ve todo. Muestra quién
tocó qué: quién movió una tarea y entre qué etapas, quién la asignó y a
quién, quién marcó un bloqueo, quién editó cada campo. Filtra por proyecto, tipo de acción
(movimientos, asignaciones, bloqueos, campos, altas), autor —personas, agente de IA o sistema— y
período, con búsqueda por texto.

Lo que hizo el agente aparece marcado y con el check-in del que salió, que es lo que hace
auditable a la IA. El feed respeta la visibilidad: no sirve de nada tapar un tablero si la
actividad cuenta los títulos de sus tareas.

El historial por tarea sigue en su panel de detalle, y ahora los eventos de asignación **dicen el
nombre** en vez de «reasignó el item».

## Borrar

| | Cómo |
|---|---|
| **Tarea** | Botón en el panel de detalle, del líder o del admin. Confirma repitiendo el identificador. Se lleva comentarios, entregables e historial. Para trabajo que efectivamente pasó, lo correcto es cerrarlo moviéndolo a una etapa final. |
| **Proyecto** | Papelera en la lista, solo admin. Pide escribir la clave y avisa cuántas tareas se van. |
| **Persona** | Solo si **no dejó rastro**. Si trabajó, el servidor lo rechaza diciendo exactamente qué dejó («2 tareas creadas, 3 cambios, 1 check-in…») y hay que desactivarla: el historial seguiría nombrando a un fantasma. |
| **Plantilla** | Papelera, salvo las de fábrica: el arranque las vuelve a sembrar. El botón se dibuja igual, deshabilitado y con el motivo — un botón ausente no explica nada. |

## Proyectos archivados

Archivar es el «se terminó» de un proyecto: sale de la lista por defecto, **deja de admitir
trabajo nuevo** —si se pudiera seguir cargando, sería solo una etiqueta— y queda registrado quién
lo archivó y cuándo. No se borra nada: el historial es el registro de lo que hizo el equipo.

La lista de proyectos muestra **los activos por defecto**, con búsqueda por clave, nombre o
descripción, y filtros por estado, disciplina y «donde participo».

**Plantillas** (`/plantillas`, solo Admin). Crear una disciplina que no venía de fábrica —un
estudio jurídico, uno de arquitectura— sin tocar código: etapas con su categoría, transiciones
válidas, campos personalizados con pistas para el agente, vocabulario y contexto de disciplina.

La validación corre **al guardar la plantilla**, no al crear el proyecto: una transición hacia una
etapa que no existe, una etapa sin salida o una plantilla sin etapa de cierre producen tableros
con tareas atrapadas, y descubrirlo tres semanas después con trabajo real adentro es mucho peor
que un mensaje de error ahora. Renombrar una etapa en el editor arrastra las transiciones que le
apuntaban.

Las de fábrica se pueden editar pero no borrar ni cambiarles la clave —el arranque las vuelve a
sembrar por clave, y renombrarla dejaría dos—: para partir de una, se duplica.

**Configuración** (`/configuracion`, solo Admin). Todo lo ajustable de la instancia —escalera de
entrega, SMTP, modelo de IA, almacenamiento, duración de la sesión, defaults de check-in— se
edita desde ahí y **aplica en caliente**: los consumidores leen por `IOptionsMonitor` y guardar
recarga la configuración. Verificado bajando el intervalo del barrido de 30 s a 5 s desde la API
y midiendo cuatro barridos en 16 segundos, sin reiniciar.

La tabla `AppSettings` se registra como la **última** fuente de configuración, así que pisa a
`appsettings.json` y a las variables de entorno. Al revés, guardar no tendría efecto y la
pantalla mentiría. Las claves y contraseñas se guardan cifradas con AES-GCM y **no vuelven nunca
por la API**: la interfaz muestra si están definidas, no su valor.

Lo único que queda fuera, y por qué: la **cadena de conexión** y la **clave de firma de los
JWT** se necesitan antes de poder leer esa tabla; la **carpeta de archivos**, los **orígenes
CORS** y el **bootstrap del primer admin** se aplican al arrancar, y moverlos en caliente
dejaría los archivos subidos apuntando a otro lado o cortaría las sesiones abiertas.

**Mensajes** (`/mensajes`). Hilos 1:1 que se entregan por los mismos canales que el resto: toast
del escritorio si la app está viva, email con enlace directo si no.

Se le puede escribir a **quien tiene trabajo asignado en los proyectos que uno lidera**, y
responderle a quien escribió primero. Nada más. La regla sale de para qué existe el canal:
escribirle a alguien es sobre el trabajo que le diste. Sin eso, cualquier cuenta con rol de
manager le escribía a toda la instancia. El admin queda afuera de la restricción, como en todo
lo demás.

## A quién le habla el agente

Al que **ejecuta** el trabajo, y a nadie más. El check-in diario se crea solo para quien:

- no es Admin,
- no lidera ningún proyecto, y
- tiene al menos una tarea abierta asignada.

La razón: el check-in existe para preguntarle a la persona que tiene la tarea en la mano cómo
viene. Al líder el agente lo ayuda del otro lado —escribiéndole a cada responsable y avisándole
lo que sale de esas charlas—; preguntarle a quien reparte el trabajo «¿cómo venís?» no tiene a
quién responderle. Y sin trabajo asignado, el check-in es una interrupción diaria sin tema.

El aviso **nombra las tareas**: «Buen día X. Hablemos de VID-31 vencida, DIS-88 bloqueada y 2
más.» Un globo que dice «¿repasamos tu trabajo?» obliga a abrir la app para saber si vale la
pena; nombrar lo que está atrasado o trabado lo convierte en información. Es la misma razón por
la que el agente abre la conversación con datos y no con «¿cómo vas?».

### Diario o solo cuando hace falta

`CheckIns:Mode` en Configuración:

- **`diario`** — todos los días laborables a quien tenga trabajo abierto.
- **`cuando-hace-falta`** — solo si hay algo que lo justifique: una tarea **vencida** y todavía
  abierta, una **bloqueada** sin resolver, o una **sin novedades** hace más de `CheckIns:StaleDays`
  días.

La segunda es la que persigue el problema real: **el bot actúa cuando el tablero dejó de contar
la verdad**. Si todo está al día no aparece, y callarse vale más que el ritual — un bot que
interrumpe sin motivo se ignora, y con él se ignora el día que sí importaba.

## Qué puede hacer el responsable con su tarea

Se decide **al asignarla**, tarea por tarea, y lo cambia el líder desde el panel de la tarea:

| Permiso | Por defecto | Por qué |
|---|---|---|
| Mover de etapa | **sí** | Es el trabajo diario de quien la ejecuta, y lo que alimenta al agente. |
| Editar título, fechas y campos | **no** | Editar el enunciado es cambiar el encargo, y el encargo es de quien lo dio. Un responsable que puede reescribir su tarea puede hacer que siempre parezca cumplida. |
| Borrar la tarea | **no** | Se lleva el historial. |

Verificado con sesiones reales: por defecto el responsable mueve (permitido), edita (403) y borra
(403); al quitarle mover y darle editar, se invierte; **no puede ampliarse los permisos a sí
mismo** (403); y cuando el líder le habilita borrar, borra. Cada cambio de permiso queda en el
historial como `permiso:mover`, `permiso:editar` o `permiso:borrar`.

La tarjeta del tablero respeta lo mismo: si no podés moverla, no se arrastra.

## La app de escritorio hace dos cosas

Chat y notificaciones. **No replica el tablero**, ni el detalle de tareas, ni la configuración, y
eso no queda librado a la buena voluntad: el WebView2 solo puede navegar a `/agent-chat`,
`/checkin/:id` y `/mensajes`. Cualquier otra ruta —un enlace a una tarea, a un entregable, al
tablero— se cancela adentro y se abre en el navegador del sistema, igual que los `target=_blank`.

La ventana tiene una barra fija con el botón **«Ir al sistema web»**, que vive en el chrome de la
app y no dentro de la página, para que siga estando aunque el WebView2 no cargue. El menú de la
bandeja ofrece «Check-in ahora», «Mensajes» e «Ir al sistema web»; el tablero ya no se abre
adentro.

**Entrar y quedarse adentro.** La app tiene su propia pantalla de ingreso: mismo usuario y
contraseña que la web, más la dirección del servidor. Antes pedía «pegá el token del
dispositivo» y **no ofrecía ningún campo donde pegarlo** — había que editar un JSON a mano.

Al entrar, la app registra el equipo y guarda un **token de dispositivo que no vence**, con el
que renueva la sesión sola en cada arranque. La contraseña se pide una sola vez. Ese token se
revoca desde la web sin tocar la contraseña de la persona, que es la diferencia con dejar la
sesión eterna. La sesión también se le inyecta al chat embebido, así no hay que entrar dos veces.

**La dirección es una sola, y apunta a la web, no a la API.** Es el error que dejó la app
mostrando una ventana en blanco: un `settings.json` viejo con `ServerUrl` en el puerto de la API
(`:5102`) pedía `/agent-chat` ahí, recibía un **404** y el WebView2 no tiene nada que dibujar.
Peor todavía, ese archivo traía además una clave `WebUrl` propia, de cuando era un ajuste
guardado; al volverse una propiedad calculada (`WebUrl => ServerUrl`) su valor dejó de aplicarse
**sin que nada lo dijera**. Ahora lleva `[JsonIgnore]`, así que no se vuelve a escribir.

Tres cosas cambiaron para que eso no pueda volver a verse como «no anda»:

- **Un fallo de carga se muestra.** El WebView2 no lanza excepción cuando el servidor contesta
  404 ni cuando no contesta, así que el `try/catch` que había nunca se enteraba. Ahora se escucha
  `NavigationCompleted` y se miran las dos cosas: el error de red **y el código HTTP**, porque
  para el WebView2 un 404 es una navegación exitosa.
- **El error tiene salida.** Antes el campo de dirección vivía solo en la pantalla de entrada, y
  esa pantalla solo aparece cuando no hay sesión: con sesión válida y dirección mala, la app
  quedaba en blanco para siempre y sin dónde arreglarla salvo borrar el JSON a mano. Ahora el
  panel de error muestra la dirección que se intentó y ofrece corregirla **sin volver a pedir la
  contraseña**, porque el problema no es de identidad.
- **Un error de arranque se cuenta.** `OnStartup` es `async void`: lo que se escapara después del
  primer `await` terminaba el proceso antes de mostrar ventana alguna. Hay manejadores globales,
  y el detalle queda en `%APPDATA%\Borlaro TMS\errores.log`.

**Ojo con `localhost`:** Caddy le saca certificado y redirige a HTTPS, y el WebView2 corta por
certificado. Usá el nombre o la IP con la que se publica la instalación.

**El clic en la notificación abre lo que menciona.** El aviso trae la ruta desde el servidor; si
es chat se abre en la app, y si es el tablero o una tarea se abre en el navegador, con
`?item=<id>` para que caiga en la tarea concreta y no en el tablero a buscarla. Antes el globo
era informativo: contaba que algo pasó y dejaba a la persona buscándolo a mano.

**Pendientes conocidos:**

- **La app de escritorio sigue sin probarse con interfaz gráfica.** Compila y la lógica de
  ingreso, sesión persistente y navegación está escrita, pero no hay forma de verificar ventana,
  bandeja, globos ni WebView2 desde esta máquina sin sesión gráfica. Es lo primero a probar a
  mano: `dotnet run --project src/Borlaro.Tms.Desktop`.
- **Falta el canal de Slack** (Fase 4b), la alternativa al escritorio para quien no puede
  instalar software o no usa Windows.


- **La app de escritorio recibe avisos; falta verificar lo que se dibuja.** El circuito de
  entrega está probado de punta a punta: la escalera despachó por SignalR y **la app acusó recibo
  sola** —el `DeliveredAt` con `DeliveredVia=Desktop` lo escribe únicamente `AcknowledgeDelivery`,
  que llama el cliente al recibir el mensaje—, así que el globo se disparó de verdad. La causa de
  la ventana en blanco está identificada y corregida (ver arriba). Lo que sigue sin verificarse
  porque requiere a alguien frente a la pantalla: que el icono de bandeja y el globo se vean, que
  el chat se dibuje dentro del WebView2, el minimizar-a-bandeja y el autoarranque. **Prueba
  manual: doble clic en el icono de la bandeja debería abrir el chat del día ya autenticado.**
- **El drag & drop no está verificado.** El código está escrito y compila, pero el panel del
  navegador de pruebas no compone frames, y dnd-kit necesita mediciones de layout durante el
  arrastre. Hay que probarlo a mano. El selector de etapa del panel de detalle cubre la misma
  operación y sí está verificado — y además es la vía accesible por teclado.
- **El agente no se probó contra Claude.** `ANTHROPIC_API_KEY` está vacía. La instalación corre
  contra un modelo local (`qwen3-taskadmin`, un Qwen3-Coder-30B-A3B en Q3 por Ollama), que
  conversa en castellano, llama herramientas correctamente y repregunta en vez de inventar. Lo
  que falta es comparar la calidad frente a `claude-opus-5`.
- **El agente puede alucinar en su primer mensaje, aunque no escriba mal.** Se lo vio narrar una
  tarea inexistente en el mismo turno en que llamaba a `get_assigned_tasks`: redactó antes de
  leer el resultado. La herramienta lo corrigió y no tocó el tablero, pero la persona igual leyó
  la frase falsa. El prompt ahora le pide llamar la herramienta sin escribir nada en ese turno,
  y con eso desapareció; queda como riesgo latente si se cambia de modelo, porque es una
  conducta que el prompt desalienta pero la arquitectura no impide.
- **Faltan el digest diario y el chat del manager**, las dos piezas de la Fase 4 que miran al
  equipo entero en vez de a una persona.
- **Cambiar el material de `Secrets:Key`** —o la clave de firma de los JWT, que es su respaldo—
  vuelve ilegibles los secretos ya guardados en Configuración. La instancia arranca igual y los
  muestra como «no definidos» para que el admin los recargue, pero no hay recuperación.
- **Una tarea tiene un solo responsable.** El modelo no soporta co-asignados; para trabajo
  compartido hoy hay que dividirlo en dos tareas. Agregarlo toca el modelo, los filtros y las
  herramientas del agente.
- **Cambiar la contraseña de alguien no cierra sus sesiones abiertas**: los tokens ya emitidos
  siguen valiendo hasta que vencen. Hace falta una lista de sesiones revocadas que hoy no existe.
- Quedaron proyectos y plantillas de prueba en la base (`VID*`, `DEV*`, `LEG`). Se pueden borrar.

## Requisitos

**Para desplegar alcanza con Docker.** No hace falta .NET ni Node en la máquina: las dos imágenes
se construyen adentro. .NET SDK 8 y Node 20+ solo hacen falta para iterar sobre el código.

## Estructura

```
src/
  Borlaro.Tms.Domain/          Entidades y reglas. Sin dependencias de infraestructura.
  Borlaro.Tms.Infrastructure/  EF Core, DbContext, migraciones, seed.
  Borlaro.Tms.Agent/           Integración con la API de Anthropic (SDK oficial de C#).
  Borlaro.Tms.Api/             ASP.NET Core: REST, auth, SignalR.
  Borlaro.Tms.Desktop/         App WPF de bandeja. Se distribuye aparte, no va en ninguna imagen.
deploy/Caddyfile             Reverse proxy y TLS automático.
deploy/web.Dockerfile        Compila el frontend y lo hornea en la imagen de Caddy.
docker-compose.yml           postgres + api + web (Caddy).
```

## Puesta en marcha

Todo el sistema corre en Docker: base, API y frontend.

```bash
cp .env.example .env
```

Completá `JWT_SIGNING_KEY` (mínimo 32 caracteres), `POSTGRES_PASSWORD` y las dos de
`BOOTSTRAP_ADMIN_*` —sin esas últimas la instancia arranca y queda inaccesible, porque **no hay
credenciales de fábrica** a propósito—. Después:

```bash
docker compose up -d --build
```

### Cómo se llega a la app

`TASKADMIN_DOMAIN` decide a qué responde Caddy, y de eso depende si hay HTTPS de verdad:

| Valor | Qué pasa | Cuándo usarlo |
|---|---|---|
| `:80` | HTTP plano, responde a cualquier host o IP | Red local. **Las contraseñas viajan sin cifrar dentro de la red.** |
| `localhost` | HTTPS con la CA interna de Caddy | Solo en el equipo donde corre; el navegador avisa del certificado |
| `taskadmin.tudominio.com` | **HTTPS real, con certificado de Let's Encrypt automático** | Expuesto a internet |

El Caddyfile sirve además `https://localhost` en paralelo, siempre. No es un lujo: al haber
servido HTTPS una vez, el navegador recuerda por HSTS que a ese host hay que pedirle HTTPS
*siempre*, y si desapareciera, `http://localhost` se convertiría solo en `https://localhost` y no
abriría nada.

Para la red local hace falta además abrir el puerto en Windows Firewall (PowerShell como
administrador):

```powershell
New-NetFirewallRule -DisplayName "Borlaro TMS HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
```

Al arrancar, la API aplica las migraciones, siembra las plantillas y crea el primer admin.

### Exponerlo a internet con HTTPS

Caddy saca y renueva el certificado solo; lo que hay que resolver es que le llegue el tráfico.
Dos caminos:

**a) Dominio propio + reenvío de puertos.** En `.env`:

```
TASKADMIN_DOMAIN=taskadmin.tudominio.com
PUBLIC_BASE_URL=https://taskadmin.tudominio.com
```

Hace falta: un registro DNS `A` apuntando a tu IP pública, reenviar **80 y 443** del router al
servidor, y abrir esos puertos en el firewall. El 80 no es opcional aunque no lo uses: Let's
Encrypt lo necesita para validar el dominio. Con IP residencial dinámica hace falta DNS dinámico,
o el certificado deja de renovarse cuando cambie la IP.

**b) Túnel de Cloudflare — recomendado si el servidor está en una oficina o casa.** No se abre
ningún puerto ni hace falta IP fija: un cliente sale desde el servidor hacia Cloudflare y el
tráfico entra por ahí, con HTTPS incluido. Es gratis, y deja el servidor sin superficie expuesta
a internet, que es una diferencia de seguridad grande frente a publicar el router.

**Antes de exponerlo, mirá tres cosas:** que `JWT_SIGNING_KEY` y `SECRETS_KEY` sean largas y
propias de esta instalación, que la contraseña del admin no sea la de las pruebas, y que
`PUBLIC_BASE_URL` coincida con el dominio real — de ahí salen los enlaces de los emails.

### Servirlo desde una máquina propia

Es el modo en que corre hoy: la instalación vive en una PC de casa y sale por un túnel de
Cloudflare. Sirve para producción chica, y es la única forma de que el agente use un modelo local
sin pagar una GPU en la nube.

**1. El túnel.** Con `cloudflared` instalado y una cuenta de Cloudflare que administre tu dominio:

```bash
cloudflared tunnel login
```

```bash
cloudflared tunnel create taskadmin
```

En `%USERPROFILE%\.cloudflared\config.yml`, agregá la entrada apuntando a Caddy —que escucha en
el 80— **antes** de la regla `http_status:404`, que es el descarte final:

```yaml
ingress:
  - hostname: taskadmin.tudominio.com
    service: http://localhost:80
  - service: http_status:404
```

```bash
cloudflared tunnel route dns taskadmin taskadmin.tudominio.com
```

Y que arranque solo, como servicio de Windows:

```bash
cloudflared service install
```

Si ya tenés un túnel sirviendo otro dominio, **no crees uno nuevo**: agregá tu `hostname` al
`ingress` del que ya existe. Un solo `cloudflared` sirve todos los que quieras.

**2. El `.env`.** Con el túnel terminando el TLS, Caddy no necesita sacar certificado:

```
TASKADMIN_DOMAIN=:80
PUBLIC_BASE_URL=https://taskadmin.tudominio.com
```

`PUBLIC_BASE_URL` con `https` aunque Caddy hable HTTP: es la URL que ve la persona, y de ahí
salen los enlaces de verificación de los emails.

**3. Respaldos, energía y arranque.** Como administrador:

```bash
powershell -ExecutionPolicy Bypass -File deploy\instalar-servidor-casero.ps1
```

Deja un respaldo diario a las 03:00 en `C:\Respaldos\Borlaro TMS` (volcado de Postgres en formato
`custom` más un `.tar.gz` de los archivos subidos, reteniendo 14 días y **verificando** que el tar
se pueda leer), apaga la suspensión y la hibernación, evita que cerrar la tapa mate el servicio, y
pone a Docker Desktop a arrancar con la sesión.

La tarea corre **como tu usuario y solo con sesión iniciada**, no como SYSTEM: Docker Desktop
levanta su daemon dentro de la sesión del usuario, así que una tarea como SYSTEM no encontraría el
pipe y fallaría todas las noches sin decir nada.

Para restaurar:

```bash
docker exec -i taskadmin-postgres pg_restore -U taskadmin -d taskadmin --clean --if-exists < taskadmin_AAAA-MM-DD_HHMM.dump
```

**Lo que el script no hace, y hay que resolver a mano:**

- **Sacar los respaldos de esta máquina.** Una copia en el mismo disco que la base no protege del
  único escenario que importa: que ese disco muera. Un `robocopy` a un disco externo o a la nube,
  en la misma tarea programada, alcanza.
- **Los reinicios de Windows Update**, que tiran el servicio a mitad de jornada. Conviene
  configurar las horas activas.
- **Que la máquina quede con sesión iniciada.** Es un requisito de Docker Desktop, no del script.

**Y el límite honesto de este modo:** un corte de luz o de internet en tu casa es una caída de la
plataforma, sin redundancia y sin nadie de guardia. Para uso interno y clientes chicos es un
intercambio razonable; para un cliente que exija disponibilidad, no.

### Actualizar

```bash
docker compose up -d --build
```

Reconstruye lo que cambió y reemplaza los contenedores. El frontend se compila **dentro** de la
imagen, así que no hay forma de desplegar una API nueva con el frontend viejo. Las migraciones
pendientes se aplican solas al arrancar.

Los entregables subidos, los emails del modo disco y la base viven en volúmenes (`uploads`,
`outbox`, `pgdata`): sobreviven a los rebuilds. `docker compose down -v` los borra.

### Iterar sobre el código

El compose publica la API en `127.0.0.1:5102`, así que se puede levantar solo el dev server del
frontend contra la instancia dockerizada:

```bash
npm run dev --prefix web
```

Frontend en `http://localhost:5173` con proxy de `/api` y `/hubs`; Swagger en
`http://localhost:5102/swagger`. Para tocar el backend, `docker compose stop api` y correrlo
desde el SDK con `dotnet run --project src/Borlaro.Tms.Api --launch-profile http`.

Corriendo la API fuera de Docker, los secretos van en user-secrets y no en `appsettings.json`,
que está versionado:

```bash
dotnet user-secrets --project src/Borlaro.Tms.Api set "Jwt:SigningKey" "<clave de 32+ caracteres>"
```

El primer administrador se crea solo si no hay ningún usuario y están definidos
`Bootstrap:AdminEmail` y `Bootstrap:AdminPassword`. **No hay credenciales por defecto**: sin esa
configuración la app arranca, avisa en el log y queda inaccesible, que es preferible a un
`admin/admin` de fábrica esperando a que alguien publique la instancia.

## Migraciones

```bash
dotnet dotnet-ef migrations add <Nombre> --project src/Borlaro.Tms.Infrastructure --startup-project src/Borlaro.Tms.Infrastructure --output-dir Migrations
```

La fábrica de diseño (`BorlaroTmsDbContextFactory`) permite generar migraciones sin la API
levantada ni Postgres corriendo.

## Decisiones que conviene conocer antes de tocar el código

- **Nada se filtra a mano por organización.** Toda entidad que implemente `IOrganizationScoped`
  la lleva como columna, y el DbContext aplica el filtro y completa el valor al guardar. Una
  tabla nueva solo tiene que implementar la interfaz. Lo que corre fuera de un request —el
  barrido de check-ins, el arranque, el formulario público antes de resolver su token— tiene que
  declararlo con `OrganizationScope.UseSystem()` o `Use(id)`: sin eso no ve nada y no puede
  escribir, que es el resultado correcto para un camino que se olvidó de decir de quién es.
- **El filtro de organización lee una propiedad de instancia del DbContext, no una estática.**
  Con una estática, EF la incrusta como constante en el SQL y cachea esa consulta: la segunda
  organización recibiría el SQL de la primera. Detalle completo en [§9 del plan](docs/PLAN.md).
- **La entidad se llama `WorkItem`, no `Task`.** `Task` colisiona con
  `System.Threading.Tasks.Task` y obligaría a calificar el tipo en cada método `async`. El
  nombre además encaja mejor: una pieza de diseño o un corte de video no son "tasks".
- **Los campos personalizados van en `jsonb` con índice GIN**, no en tablas EAV. Un proyecto
  puede definir cualquier campo sin migración, y los filtros se resuelven por índice.
- **Los enums se persisten como texto.** Reordenar un enum no corrompe datos existentes y las
  filas se leen sin diccionario.
- **Las transiciones de etapa se validan en el dominio, no en el prompt del agente.** La IA no
  puede saltárselas ni "convencer" al sistema.
- **Toda acción de la IA queda en `WorkItemEvent` con `ActorType.Agent` y el `CheckInId`** que
  la originó: trazable y reversible.
- **Las plantillas de disciplina son datos, no código.** Se siembran de fábrica y se editan o se
  crean nuevas desde `/plantillas`, sin tocar el binario ni la base.
