import type { UserRole } from '@/stores/auth'

export const USER_ROLE_LABEL: Record<UserRole, string> = {
  Admin: 'Admin',
  Manager: 'Líder de equipo',
  Collaborator: 'Colaborador',
  ClientReviewer: 'Cliente revisor',
  PlatformOperator: 'Operador de la plataforma',
}

/** Quién puede ser responsable de un proyecto. Liderar es un rol de la instancia, no algo que se
 *  reparta por tablero: si alguien no es Admin ni Manager, primero hay que cambiarle el rol en
 *  Personas. El backend lo valida igual; esto evita ofrecer lo que va a rechazar. */
export const canLead = (role: UserRole) => role === 'Admin' || role === 'Manager'

/** Iniciales para el avatar. Dos como máximo: con tres deja de leerse como iniciales y empieza
 *  a parecer una sigla. */
export function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}
