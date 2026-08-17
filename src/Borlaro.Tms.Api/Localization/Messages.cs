using System.Security.Claims;

namespace Borlaro.Tms.Api.Localization;

/// <summary>Los textos que el servidor le manda a una persona: errores de la API y cuerpos de
/// email.
///
/// Es un diccionario en código y no archivos `.resx` por una razón concreta: son unas pocas
/// decenas de frases, todas de una pantalla u otra, y un `.resx` agrega un editor aparte, un
/// generador y un ciclo de compilación para resolver un problema que acá no existe. Si algún día
/// esto crece a cientos de cadenas o entra un equipo de traducción, mudarlo es mecánico.
///
/// Solo vive acá lo que **lee una persona**. Los mensajes de log siguen en castellano: los lee
/// quien opera el servidor, no el usuario, y traducirlos haría más difícil buscarlos.</summary>
public static class Messages
{
    /// <summary>El idioma de reserva: el que se usa cuando no se pudo determinar otro, y al que se
    /// cae si a un idioma le faltara una frase.</summary>
    public const string DefaultLanguage = "en";

    public static readonly string[] Supported = ["es", "en", "pt"];

    public enum Key
    {
        InvalidCredentials,
        OrganizationSuspended,
        AccountInManyOrganizations,
        DeviceUnlinked,
        SignUpInvalidEmail,
        SignUpDomainNotAllowed,
        SignUpShortPassword,
        SignUpNeedsOrganizationName,
        SignUpLinkExpired,
        SignUpEmailTaken,
        SignUpClosed,
        VerifySubject,
        VerifyBody,
        AlreadyRegisteredSubject,
        AlreadyRegisteredBody,
    }

    private static readonly Dictionary<string, Dictionary<Key, string>> Catalog = new()
    {
        ["es"] = new()
        {
            [Key.InvalidCredentials] = "Credenciales inválidas.",
            [Key.OrganizationSuspended] = "La organización está suspendida. Escribinos para reactivarla.",
            [Key.AccountInManyOrganizations] =
                "Esta dirección tiene cuenta en más de una organización. Todavía no está implementado elegir a cuál entrar.",
            [Key.DeviceUnlinked] = "Este equipo ya no está vinculado. Entrá de nuevo con tu usuario y contraseña.",
            [Key.SignUpInvalidEmail] = "Ese email no parece válido.",
            [Key.SignUpDomainNotAllowed] = "Esta instalación solo admite altas de dominios habilitados.",
            [Key.SignUpShortPassword] = "La contraseña debe tener al menos {min} caracteres.",
            [Key.SignUpNeedsOrganizationName] = "La organización necesita un nombre.",
            [Key.SignUpLinkExpired] = "Este enlace venció o ya se usó. Volvé a registrarte y te mandamos uno nuevo.",
            [Key.SignUpEmailTaken] = "Esa dirección ya tiene cuenta. Entrá con tu contraseña.",
            [Key.SignUpClosed] = "El registro está cerrado en esta instalación.",
            [Key.VerifySubject] = "Confirmá tu cuenta de Borlaro TMS",
            [Key.VerifyBody] =
                "Hola {name}. Para terminar de crear «{organization}» confirmá esta dirección con el enlace de abajo. Vence en 24 horas.",
            [Key.AlreadyRegisteredSubject] = "Ya tenés cuenta en Borlaro TMS",
            [Key.AlreadyRegisteredBody] =
                "Alguien intentó abrir una organización con esta dirección, que ya tiene cuenta. Si fuiste vos, entrá con tu contraseña de siempre. Si no, podés ignorar este mensaje: no se creó nada.",
        },

        ["en"] = new()
        {
            [Key.InvalidCredentials] = "Invalid credentials.",
            [Key.OrganizationSuspended] = "The organization is suspended. Get in touch to reactivate it.",
            [Key.AccountInManyOrganizations] =
                "This address has an account in more than one organization. Choosing which one to enter is not implemented yet.",
            [Key.DeviceUnlinked] = "This machine is no longer linked. Sign in again with your username and password.",
            [Key.SignUpInvalidEmail] = "That email doesn't look valid.",
            [Key.SignUpDomainNotAllowed] = "This installation only accepts sign-ups from allowed domains.",
            [Key.SignUpShortPassword] = "The password must be at least {min} characters long.",
            [Key.SignUpNeedsOrganizationName] = "The organization needs a name.",
            [Key.SignUpLinkExpired] = "This link expired or was already used. Sign up again and we'll send a new one.",
            [Key.SignUpEmailTaken] = "That address already has an account. Sign in with your password.",
            [Key.SignUpClosed] = "Sign-up is closed on this installation.",
            [Key.VerifySubject] = "Confirm your Borlaro TMS account",
            [Key.VerifyBody] =
                "Hi {name}. To finish creating “{organization}”, confirm this address with the link below. It expires in 24 hours.",
            [Key.AlreadyRegisteredSubject] = "You already have a Borlaro TMS account",
            [Key.AlreadyRegisteredBody] =
                "Someone tried to open an organization with this address, which already has an account. If that was you, sign in with your usual password. If not, you can ignore this message: nothing was created.",
        },

        ["pt"] = new()
        {
            [Key.InvalidCredentials] = "Credenciais inválidas.",
            [Key.OrganizationSuspended] = "A organização está suspensa. Fale conosco para reativá-la.",
            [Key.AccountInManyOrganizations] =
                "Este endereço tem conta em mais de uma organização. Escolher em qual entrar ainda não está implementado.",
            [Key.DeviceUnlinked] = "Este computador não está mais vinculado. Entre de novo com seu usuário e senha.",
            [Key.SignUpInvalidEmail] = "Esse e-mail não parece válido.",
            [Key.SignUpDomainNotAllowed] = "Esta instalação só aceita cadastros de domínios habilitados.",
            [Key.SignUpShortPassword] = "A senha precisa ter pelo menos {min} caracteres.",
            [Key.SignUpNeedsOrganizationName] = "A organização precisa de um nome.",
            [Key.SignUpLinkExpired] = "Este link venceu ou já foi usado. Cadastre-se de novo e enviaremos outro.",
            [Key.SignUpEmailTaken] = "Esse endereço já tem conta. Entre com sua senha.",
            [Key.SignUpClosed] = "O cadastro está fechado nesta instalação.",
            [Key.VerifySubject] = "Confirme sua conta do Borlaro TMS",
            [Key.VerifyBody] =
                "Olá {name}. Para terminar de criar “{organization}”, confirme este endereço com o link abaixo. Vence em 24 horas.",
            [Key.AlreadyRegisteredSubject] = "Você já tem conta no Borlaro TMS",
            [Key.AlreadyRegisteredBody] =
                "Alguém tentou abrir uma organização com este endereço, que já tem conta. Se foi você, entre com sua senha de sempre. Se não, pode ignorar esta mensagem: nada foi criado.",
        },
    };

    public static string Get(string? language, Key key, params (string Name, object Value)[] values)
    {
        var dictionary = Catalog.GetValueOrDefault(Normalize(language)) ?? Catalog[DefaultLanguage];

        // Si a un idioma le faltara una clave, cae al de reserva: una frase en otro idioma se
        // entiende, el nombre del enum no.
        var text = dictionary.GetValueOrDefault(key) ?? Catalog[DefaultLanguage][key];

        foreach (var (name, value) in values)
        {
            text = text.Replace($"{{{name}}}", value.ToString());
        }

        return text;
    }

    /// <summary>«pt-BR», «PT» y «pt» son lo mismo para lo que nos importa acá.</summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return DefaultLanguage;

        var code = language.Trim().ToLowerInvariant();
        if (code.Length > 2) code = code[..2];

        return Supported.Contains(code) ? code : DefaultLanguage;
    }
}

public static class LanguageExtensions
{
    /// <summary>En qué idioma contestarle a quien llama.
    ///
    /// Primero el claim, porque es la elección explícita de esa persona y viaja firmada; después
    /// el `Accept-Language`, que es lo único que hay cuando todavía no hay sesión —el alta y el
    /// login, justamente donde peor cae contestar en un idioma que no se entiende—.</summary>
    public static string Language(this HttpContext context)
    {
        var claim = context.User.FindFirstValue(Borlaro.Tms.Api.Auth.TokenService.LanguageClaim);
        if (!string.IsNullOrWhiteSpace(claim)) return Messages.Normalize(claim);

        var header = context.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(header)) return Messages.DefaultLanguage;

        // «es-AR,es;q=0.9,en;q=0.8» → el primero que hablemos. No se ordena por `q` a propósito:
        // el navegador ya los manda en orden de preferencia.
        foreach (var part in header.Split(','))
        {
            var code = Messages.Normalize(part.Split(';')[0]);
            if (part.TrimStart().StartsWith(code, StringComparison.OrdinalIgnoreCase)) return code;
        }

        return Messages.DefaultLanguage;
    }
}
