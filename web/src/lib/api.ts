/** Error de API con el status y el detalle del ProblemDetails, para que la UI pueda
 *  distinguir un 401 de un 409 sin parsear mensajes. */
export class ApiError extends Error {
  readonly status: number
  readonly detail?: unknown

  constructor(status: number, message: string, detail?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
  }
}

let authToken: string | null = null

export function setAuthToken(token: string | null) {
  authToken = token
}

export function getAuthToken() {
  return authToken
}

type RequestOptions = Omit<RequestInit, 'body'> & { body?: unknown }

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { body, headers, ...rest } = options

  // El idioma elegido viaja en cada request. Con sesión el servidor lo lee del token, pero el
  // alta y el login son anónimos y ahí este header es lo único que hay — y son justo las
  // pantallas donde peor cae recibir un error en un idioma que no se entiende. Se lee de
  // localStorage y no del contexto de React porque esto es un módulo, no un componente.
  const language = localStorage.getItem('taskadmin.lang')

  const response = await fetch(path.startsWith('/') ? path : `/api/${path}`, {
    ...rest,
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(authToken ? { Authorization: `Bearer ${authToken}` } : {}),
      ...(language ? { 'Accept-Language': language } : {}),
      ...headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (response.status === 204) {
    return undefined as T
  }

  const isJson = response.headers.get('content-type')?.includes('json')
  const payload = isJson ? await response.json().catch(() => null) : await response.text()

  if (!response.ok) {
    const message =
      (payload && typeof payload === 'object' && 'detail' in payload
        ? String((payload as { detail: unknown }).detail)
        : null) ?? `Error ${response.status}`
    throw new ApiError(response.status, message, payload)
  }

  return payload as T
}

/** Igual que `api`, pero para `multipart/form-data` (subidas de archivos). No serializa a JSON:
 *  el body viaja tal cual, con el Content-Type que arma el navegador. */
export async function apiForm<T>(path: string, form: FormData): Promise<T> {
  const language = localStorage.getItem('taskadmin.lang')

  const response = await fetch(path.startsWith('/') ? path : `/api/${path}`, {
    method: 'POST',
    headers: {
      ...(authToken ? { Authorization: `Bearer ${authToken}` } : {}),
      ...(language ? { 'Accept-Language': language } : {}),
    },
    body: form,
  })

  if (response.status === 204) {
    return undefined as T
  }

  const isJson = response.headers.get('content-type')?.includes('json')
  const payload = isJson ? await response.json().catch(() => null) : await response.text()

  if (!response.ok) {
    const message =
      (payload && typeof payload === 'object' && 'detail' in payload
        ? String((payload as { detail: unknown }).detail)
        : null) ?? `Error ${response.status}`
    throw new ApiError(response.status, message, payload)
  }

  return payload as T
}
