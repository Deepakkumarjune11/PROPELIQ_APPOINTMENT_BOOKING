/**
 * Parses an ASP.NET Core `ValidationProblemDetails` response body into a flat
 * field-keyed error map suitable for `handleServerErrors`.
 *
 * Expected input shape (400 / 422 responses):
 * ```json
 * { "errors": { "Email": ["Email already exists"], "Password": ["Too short"] } }
 * ```
 *
 * Field keys are lowercased to match camelCase TypeScript form field names.
 *
 * OWASP A03 guard: only the first message per field (`v[0]`) is used — prevents
 * unbounded error string injection from server-controlled response arrays.
 *
 * @returns Field-keyed error map, or `{}` for all unrecognised/null/undefined inputs.
 */
export function parseApiErrors(data: unknown): Record<string, string> {
  if (!data || typeof data !== 'object') return {};

  const body = data as Record<string, unknown>;

  // ASP.NET Core ValidationProblemDetails: { errors: { FieldName: ["message", ...] } }
  if (body.errors && typeof body.errors === 'object' && !Array.isArray(body.errors)) {
    return Object.fromEntries(
      Object.entries(body.errors as Record<string, string[]>)
        .filter(([, v]) => Array.isArray(v) && v.length > 0)
        .map(([k, v]) => [k.toLowerCase(), v[0] ?? 'Invalid value']),
    );
  }

  return {};
}
