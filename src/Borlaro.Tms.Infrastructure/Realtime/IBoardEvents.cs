namespace Borlaro.Tms.Infrastructure.Realtime;

/// <summary>Qué le pasó a una tarea. El cliente no necesita más que esto: con la clave del
/// proyecto sabe si le interesa, y con el resto decide qué volver a pedir.</summary>
/// <param name="ProjectKey">La clave del proyecto, que es el alcance de la difusión.</param>
/// <param name="ItemKey">El identificador legible —DEV-142—, para poder mostrarlo sin otra consulta.</param>
/// <param name="Kind">created | updated | moved | deleted.</param>
/// <param name="ByAgent">Si lo hizo el agente de IA. La interfaz lo distingue de un cambio de una
/// persona, que es la misma distinción que ya hace la pantalla de Actividad.</param>
public record BoardEvent(
    string ProjectKey,
    Guid ItemId,
    string ItemKey,
    string Kind,
    bool ByAgent);

/// <summary>Avisa que el tablero de un proyecto cambió.
///
/// Vive en Infrastructure pero se implementa en el proyecto API, que es el único que tiene el
/// hub — el mismo arreglo que <c>INotificationChannel</c> y <c>DesktopChannel</c>. Sin esta
/// interfaz, `WorkItemService` tendría que conocer SignalR y la capa de dominio quedaría atada al
/// transporte.
///
/// Se difunde el hecho, no el estado: «se movió DEV-142» y no la tarea entera. Mandar el objeto
/// obligaría a serializarlo igual que la API, y a mantener dos formas del mismo dato en sincronía
/// para siempre. El cliente vuelve a pedir lo que ya sabe pedir, y aprovecha sus permisos: quien
/// no puede ver un proyecto tampoco lo recibe al refrescar.</summary>
public interface IBoardEvents
{
    Task PublishAsync(BoardEvent evento, CancellationToken ct = default);
}

/// <summary>La implementación que se usa cuando no hay hub: pruebas, o cualquier proceso que use
/// los servicios de dominio sin levantar la API. Que no haya nadie escuchando no puede hacer
/// fallar una transición de tarea.</summary>
public class NoBoardEvents : IBoardEvents
{
    public Task PublishAsync(BoardEvent evento, CancellationToken ct = default) => Task.CompletedTask;
}
