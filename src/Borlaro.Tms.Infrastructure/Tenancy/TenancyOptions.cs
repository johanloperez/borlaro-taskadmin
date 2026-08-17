namespace Borlaro.Tms.Infrastructure.Tenancy;

public enum TenancyMode
{
    /// <summary>Una sola empresa. No hay registro, ni consola de plataforma, ni forma de crear
    /// una segunda organización: la instalación entera es de quien la desplegó. Es el modo de
    /// on-premise, y el que reproduce el comportamiento que tenía el producto antes de existir
    /// las organizaciones.</summary>
    Single = 0,

    /// <summary>Varias empresas sobre la misma instalación, cada una con su gente y sus
    /// proyectos.</summary>
    Multi = 1
}

/// <summary>De cuántas empresas es esta instalación.
///
/// Es un interruptor sobre el mismo código y no dos productos. Bifurcar el on-premise en una
/// rama propia parece más simple el primer día y garantiza que al tercer mes tenga bugs que la
/// otra ya arregló, y al sexto que las dos hayan dejado de parecerse.</summary>
public class TenancyOptions
{
    public const string SectionName = "Tenancy";

    public TenancyMode Mode { get; set; } = TenancyMode.Single;

    public bool IsMulti => Mode == TenancyMode.Multi;

    /// <summary>Si cualquiera puede crear su organización entrando con el proveedor de identidad.
    /// Apagado no rompe nada: las organizaciones las sigue creando el operador desde su consola.
    /// En modo <see cref="TenancyMode.Single"/> no se mira: ahí no hay registro posible.</summary>
    public bool AllowRegistration { get; set; } = true;

    public bool RegistrationOpen => IsMulti && AllowRegistration;

    /// <summary>Dominios de email que pueden abrir una organización, separados por coma. Vacío =
    /// cualquiera. Es la diferencia entre publicar la instalación para un grupo conocido y
    /// publicarla para internet.</summary>
    public string AllowedRegistrationDomains { get; set; } = string.Empty;

    public IReadOnlyList<string> RegistrationDomains =>
        AllowedRegistrationDomains
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.TrimStart('@').ToLowerInvariant())
            .ToList();

    public bool DomainAllowed(string email)
    {
        var domains = RegistrationDomains;
        return domains.Count == 0 || domains.Contains(email.Split('@').Last().ToLowerInvariant());
    }
}
