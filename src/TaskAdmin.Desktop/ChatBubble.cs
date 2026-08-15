using System.Windows;
using System.Windows.Controls;

namespace TaskAdmin.Desktop;

public enum BubbleKind { Agente, Persona, Accion }

/// <summary>Una línea de la conversación, ya lista para dibujar.</summary>
public record ChatBubble(BubbleKind Kind, string Text);

/// <summary>Elige la plantilla según quién habló.
///
/// Un selector y no un `DataTrigger` sobre una sola plantilla porque las tres burbujas no
/// comparten forma: cambian el color, la alineación, el margen y hasta qué esquina va redondeada.
/// Expresar eso con disparadores sobre un único `Border` es más código y menos legible que tres
/// plantillas chicas.</summary>
public class BubbleTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Agente { get; set; }
    public DataTemplate? Persona { get; set; }
    public DataTemplate? Accion { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is ChatBubble burbuja
            ? burbuja.Kind switch
            {
                BubbleKind.Persona => Persona,
                BubbleKind.Accion => Accion,
                _ => Agente
            }
            : base.SelectTemplate(item, container);
}
