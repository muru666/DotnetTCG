import { HttpErrorResponse } from '@angular/common/http';

/** Error normalizado para la UI. Nunca expone stack traces ni detalles internos. */
export interface ApiError {
  readonly status: number;
  readonly code: string;
  readonly message: string;
}

interface ProblemDetails {
  readonly title?: string;
  readonly detail?: string;
  readonly errors?: Record<string, string[]>;
}

/** Traduce ProblemDetails del backend a mensajes útiles sin filtrar internos. */
export function toApiError(response: HttpErrorResponse): ApiError {
  const problem = response.error as ProblemDetails | null;
  const code = typeof problem?.title === 'string' && problem.title.length > 0
    ? problem.title
    : 'Error.Unknown';
  const fieldError = firstFieldError(problem?.errors);
  const message = fieldError
    ?? (typeof problem?.detail === 'string' && problem.detail.length > 0 ? problem.detail : null)
    ?? fallbackMessage(response.status);
  return { status: response.status, code, message };
}

function firstFieldError(errors: Record<string, string[]> | undefined): string | null {
  if (errors === undefined) {
    return null;
  }
  for (const messages of Object.values(errors)) {
    const first = messages.find((m) => m.length > 0);
    if (first !== undefined) {
      return first;
    }
  }
  return null;
}

function fallbackMessage(status: number): string {
  switch (status) {
    case 0:
      return 'Sin conexión con el servidor.';
    case 400:
      return 'La solicitud no es válida.';
    case 401:
      return 'Sesión vencida. Volvé a ingresar.';
    case 403:
      return 'No tenés permiso para esta acción.';
    case 404:
      return 'No se encontró el recurso.';
    case 409:
      return 'Conflicto con el estado actual.';
    case 429:
      return 'Demasiados intentos. Esperá un momento.';
    default:
      return 'Ocurrió un error inesperado.';
  }
}
