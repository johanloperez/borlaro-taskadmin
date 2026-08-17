import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { MarkdownImage } from '@/components/MarkdownImage'

/** Muestra Markdown escrito por una persona del equipo.
 *
 *  **Nunca HTML crudo.** `react-markdown` lo ignora por defecto y acá no se habilita
 *  `rehype-raw`: el enunciado de una tarea lo escribe alguien y lo leen todos los demás, así que
 *  permitir HTML sería dejar que quien crea una tarea ejecute algo en la pantalla del resto. Por
 *  la misma razón no se usa `dangerouslySetInnerHTML` con un renderizador propio, que es el atajo
 *  con el que suele entrar ese agujero.
 *
 *  `remark-gfm` agrega lo que la gente realmente escribe: tablas, listas de tareas, tachado y
 *  enlaces automáticos. Sin eso, un `- [ ]` se ve como texto y una tabla como un amasijo de pipes.
 *
 *  Los estilos van declarados uno por uno y no con un plugin de tipografía, porque el resto de la
 *  aplicación ya tiene sus tokens de color y tamaño: heredarlos mantiene el panel coherente y
 *  responde solo al tema claro u oscuro que el usuario tenga puesto. */
export function Markdown({ children }: { children: string }) {
  return (
    <div className="space-y-2 text-sm leading-relaxed">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          h1: (props) => <p className="text-base font-semibold" {...props} />,
          h2: (props) => <p className="text-sm font-semibold" {...props} />,
          h3: (props) => <p className="text-sm font-medium" {...props} />,
          p: (props) => <p className="whitespace-pre-wrap" {...props} />,
          ul: (props) => <ul className="list-disc space-y-1 pl-5" {...props} />,
          ol: (props) => <ol className="list-decimal space-y-1 pl-5" {...props} />,
          code: (props) => (
            <code className="rounded bg-canvas px-1 py-0.5 font-mono text-[13px]" {...props} />
          ),
          pre: (props) => (
            <pre
              className="overflow-x-auto rounded-lg border border-line bg-canvas p-2.5
                         font-mono text-[13px]"
              {...props}
            />
          ),
          blockquote: (props) => (
            <blockquote className="border-l-2 border-line pl-3 text-ink-muted" {...props} />
          ),
          // `noopener` y `_blank`: un enlace del enunciado sale del sistema, y abrirlo en la misma
          // pestaña sacaría a la persona de la tarea que está mirando.
          a: (props) => (
            <a
              className="text-accent underline underline-offset-2"
              target="_blank"
              rel="noopener noreferrer"
              {...props}
            />
          ),
          table: (props) => (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left" {...props} />
            </div>
          ),
          th: (props) => <th className="border border-line px-2 py-1 font-medium" {...props} />,
          td: (props) => <td className="border border-line px-2 py-1" {...props} />,
          // Las capturas adjuntas se sirven con sesión, así que necesitan su propio componente.
          img: (props) => <MarkdownImage src={props.src} alt={props.alt} />,
          hr: () => <hr className="border-line" />,
        }}
      >
        {children}
      </ReactMarkdown>
    </div>
  )
}
