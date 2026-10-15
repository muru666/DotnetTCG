/**
 * Extrae un mensaje legible de un error HTTP. El backend responde
 * ProblemDetails ({ title, detail }), no { message }.
 */
export function extractHttpError(err: unknown, fallback: string): string {
  const response = err as {
    error?: { message?: string; detail?: string; title?: string } | string;
    message?: string;
  };
  if (typeof response?.error === 'string') return response.error;
  return response?.error?.message
    ?? response?.error?.detail
    ?? response?.message
    ?? fallback;
}
