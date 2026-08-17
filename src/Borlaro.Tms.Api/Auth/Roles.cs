namespace Borlaro.Tms.Api.Auth;

/// <summary>Nombres de rol tal como viajan en el claim, y las políticas que los agrupan.
/// Se usan constantes para que un typo sea un error de compilación y no un 403 silencioso.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Collaborator = "Collaborator";
    public const string ClientReviewer = "ClientReviewer";
    public const string PlatformOperator = "PlatformOperator";
}

public static class Policies
{
    /// <summary>Puede administrar su organización: usuarios, plantillas, ajustes.</summary>
    public const string IsAdmin = "IsAdmin";

    /// <summary>Opera la instalación: da de alta organizaciones, las suspende y las reactiva.
    /// Deliberadamente **no** incluye al Admin de una organización: son autoridades distintas y
    /// la que manda acá está por encima de todas las empresas, no adentro de una.</summary>
    public const string IsPlatformOperator = "IsPlatformOperator";

    /// <summary>Puede crear proyectos, asignar trabajo y aprobar acciones propuestas por el
    /// agente (cambios de fecha, trabajo nuevo).</summary>
    public const string CanManage = "CanManage";

    /// <summary>Cualquier miembro del equipo con trabajo asignado.</summary>
    public const string IsTeamMember = "IsTeamMember";
}
