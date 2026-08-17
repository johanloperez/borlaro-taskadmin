# Borlaro TMS

Gestión de trabajo para equipos de cualquier disciplina, con un agente de IA que conversa cada día
con cada persona y traduce la respuesta en cambios del tablero.

**El idioma del proyecto es el español.** Código, comentarios, commits, documentación y respuestas.

---

## La regla que manda: el plan es la especificación

[docs/PLAN.md](docs/PLAN.md) no es un registro de lo que se hizo — es **el documento del que se
podría reconstruir el sistema entero**. Se usa como requerimiento de entrada para otros agentes,
así que tiene que alcanzar por sí solo, sin el código a la vista y sin esta conversación.

**Todo cambio de lógica, comportamiento, modelo de datos o función nueva se refleja en el plan
dentro de la misma tanda de trabajo en que se hace.** No al final del día, no «cuando cerremos»:
la tanda no está terminada si el plan no la incluye. Un cambio que solo vive en el código es un
requerimiento perdido.

Qué se escribe y qué no:

| Cambio | ¿Va al plan? |
|---|---|
| Función nueva, o una existente que cambia de comportamiento | **Sí** |
| Decisión de modelo de datos, o una tabla/campo nuevo con significado | **Sí** |
| Una regla de negocio, un permiso, una validación, un modo de falla cubierto | **Sí** |
| Corrección de un bug que revela que la regla escrita estaba mal | **Sí** — se corrige la regla |
| Refactor sin cambio de comportamiento, renombres, formato | No |
| Corrección de un bug que solo incumplía lo ya escrito | No |

### Cómo se escribe

Seguí la forma que ya tienen las secciones §12, §13 y §14 — es deliberada, no casual:

1. **El problema que resuelve**, antes que la solución. Quien lea el plan sin conocer el código
   tiene que entender por qué esto existe.
2. **La decisión y su porqué**, incluidas las alternativas descartadas y qué las descartó. Ese
   «por qué no» es lo que impide que alguien deshaga la decisión más adelante sin saberlo.
3. **Los nombres reales**: entidades, campos, endpoints, archivos. `WorkflowStages.DefaultAssigneeId`,
   no «un campo para el responsable».
4. **Los modos de falla cubiertos explícitamente**, y qué pasa cuando se dan.
5. **El estado al cierre**, con los marcadores de siempre:
   - `✅ **Construido y verificado**:` seguido de *cómo* se verificó, en concreto.
   - `⏳ **Pendiente:**` lo que quedó afuera a propósito.

Una función nueva de peso abre **sección numerada nueva** al final (§15, §16…). Un cambio sobre
algo ya descrito se **edita adentro de su sección** — no se agrega una sección «cambios a §12»,
porque el plan tiene que leerse como el estado actual del sistema, no como su historia.

El README lleva además una **tabla de estado**; si el cambio agrega o mueve una fila, actualizala
en la misma tanda.

---

## Arquitectura

Solución .NET 8, cuatro proyectos en `src/` más el frontend en `web/`:

| Proyecto | Qué es |
|---|---|
| `Borlaro.Tms.Domain` | Entidades y reglas. No depende de nada. |
| `Borlaro.Tms.Infrastructure` | EF Core + Npgsql, migraciones, servicios de dominio, notificaciones, storage, tenancy, seeding |
| `Borlaro.Tms.Agent` | El agente de check-ins y sus 9 herramientas. `Providers/`: Anthropic, OpenAI-compatible (Ollama/LM Studio/vLLM) y `ScriptedAgentModel` para pruebas |
| `Borlaro.Tms.Api` | Minimal API. `Endpoints/` (uno por área), `Auth/`, `Tenancy/`, `Realtime/` (SignalR), `Localization/Messages.cs` |
| `Borlaro.Tms.Desktop` | App WPF en bandeja: chat y centro de notificaciones. No es «la versión de escritorio» |
| `web/` | React + TypeScript + Vite. `pages/`, `components/`, `stores/`, `lib/`, `locales/` |

### Reglas que atraviesan todo

**Aislamiento entre organizaciones.** Todo cuelga de una `Organization` y ninguna ve nada de otra;
el filtro global de EF Core lo aplica sobre lo que implementa `IOrganizationScoped`. **`Organization`
misma no lo implementa** —es la organización, no algo suyo— así que todo endpoint que toque la tabla
`Organizations` comprueba a mano que la sesión sea de esa organización, o que sea el operador. Es la
única tabla donde el aislamiento no viene solo (§14 del plan).

**Los avisos se emiten en el servicio de dominio, nunca en el endpoint.** El agente no pasa por HTTP;
lo que se emita desde la API no le llega a nadie cuando el cambio lo hizo él. Y siempre *después* del
`SaveChanges`. En tiempo real se difunde **el hecho, no el estado**: el cliente invalida y vuelve a
pedir con sus permisos aplicados.

**Tres idiomas, con el inglés como reserva.** Interfaz en `web/src/locales/{en,es,pt}.ts`, servidor
en `src/Borlaro.Tms.Api/Localization/Messages.cs`. `en` es el diccionario de referencia y los otros dos
se tipan contra él: **una traducción que falta no compila.** Un texto nuevo se agrega en los tres.

**Nadie se borra.** Las personas se desactivan: su nombre cuelga de tareas, comentarios y transcripts.
Y la instancia no puede quedarse sin ningún admin activo.

**Configuración en caliente.** Lo que se carga desde la pantalla de Configuración se resuelve en cada
uso (el proveedor del modelo, el SMTP), así que cambiarlo no reinicia nada. Los secretos se guardan
cifrados con `Secrets__Key`.

---

## Cómo se corre

Todo junto, que es como se despliega:

```bash
docker compose up -d --build
```

Queda en `http://localhost` (o en `TASKADMIN_DOMAIN` con TLS de Caddy). La API asoma en
`127.0.0.1:5102` para apuntarle el dev server o la app de escritorio.

**El nombre viejo sobrevive en la infraestructura a propósito.** `TASKADMIN_DOMAIN`, el nombre y el
usuario de la base, los `container_name` y la carpeta de respaldos siguen diciendo `taskadmin`
porque nombran cosas que ya existen en máquinas desplegadas; renombrarlas es una migración a mano,
no un reemplazo de texto. En el código y en lo que se ve, el nombre es **Borlaro TMS** (§18 del
plan). No los «arregles» de paso.

Para trabajar en el frontend, con la API en Docker:

```bash
npm run dev --prefix web
```

Vite en `5173`, ya declarado en CORS y en `.claude/launch.json` (usalo con la herramienta de preview,
no con Bash).

API sola, contra el Postgres del compose:

```bash
dotnet run --project src/Borlaro.Tms.Api
```

Migraciones:

```bash
dotnet ef migrations add NombreDescriptivo --project src/Borlaro.Tms.Infrastructure --startup-project src/Borlaro.Tms.Api
```

Se aplican solas al arrancar la API. El `BorlaroTmsDbContextModelSnapshot.cs` va en el mismo commit
que la migración: sin él, la siguiente migración sale mal calculada.

### Configuración

`.env` a partir de `.env.example`, y está en `.gitignore` — nunca lo subas, ni pegues su contenido en
una respuesta. `TENANCY_MODE=Single` es el modo on-premise (una empresa, sin registro ni consola de
plataforma); `Multi` aloja varias. Es un interruptor sobre el mismo código, no otra versión.

Sin `Email:SmtpHost` los correos se escriben como `.eml` en `src/Borlaro.Tms.Api/outbox/`. Sin clave de
API el agente cae al modelo guionado en vez de fallar — deliberado, y la pantalla de Configuración
dice qué está corriendo de verdad.

---

## Al terminar una tanda

1. `docs/PLAN.md` refleja lo que cambió, con la forma de arriba.
2. La tabla de estado del README, si corresponde.
3. Recién ahí se cierra.

Esto no es cortesía documental: el plan es la entrada de otros agentes, y lo que no esté escrito ahí
no existe para ellos.
