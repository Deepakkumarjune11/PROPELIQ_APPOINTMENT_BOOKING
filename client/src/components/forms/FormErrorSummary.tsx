import { Alert, Link, List, ListItem, Typography } from '@mui/material';

/**
 * ## Field ID naming convention
 *
 * Use `{formName}-{fieldName}` as the DOM `id` for every form input.
 * This ensures `FormErrorSummary` can locate each field by ID for scroll + focus.
 *
 * Examples:
 * - `login-email`, `login-password`
 * - `walkin-patientName`, `walkin-appointmentDate`
 * - `createUser-email`, `createUser-role`
 *
 * Pass the same IDs both to the `<TextField id="...">` prop and to `fieldIds` here.
 */

interface FormErrorSummaryProps {
  /**
   * Active error messages keyed by field name (matches keys in `fieldIds`).
   * Fields with empty strings or `undefined` values are ignored.
   */
  errors: Partial<Record<string, string>>;
  /**
   * Maps each field key to the DOM `id` of the corresponding `<input>` element.
   * Used by `scrollAndFocus` to locate and focus the errored field.
   */
  fieldIds: Record<string, string>;
}

/**
 * Scrolls to the element with `id` and moves keyboard focus to it after the scroll
 * has settled (100ms defer — WCAG 2.4.3 Focus Order, AC-2).
 */
function scrollAndFocus(id: string): void {
  const el = document.getElementById(id);
  if (!el) return;
  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  // Defer focus so the scroll animation does not fight the focus event
  setTimeout(() => {
    document.getElementById(id)?.focus();
  }, 100);
}

/**
 * Renders an error summary `Alert` at the top of a form listing all active errors
 * as clickable buttons that scroll to and focus the corresponding field (US_039 AC-2).
 *
 * - Renders **nothing** when there are no active errors.
 * - `role="alert"` + `aria-live="assertive"` + `aria-atomic="true"` ensures the full
 *   error list is announced immediately by screen readers on submit (WCAG 4.1.3).
 * - Each error link uses `component="button"` to be keyboard-reachable (Enter activates).
 *
 * @example
 * ```tsx
 * <FormErrorSummary
 *   errors={errors}
 *   fieldIds={{ email: 'login-email', password: 'login-password' }}
 * />
 * ```
 */
export function FormErrorSummary({ errors, fieldIds }: FormErrorSummaryProps) {
  const activeErrors = Object.entries(errors).filter(
    ([, msg]) => typeof msg === 'string' && msg.length > 0,
  ) as [string, string][];

  if (activeErrors.length === 0) return null;

  return (
    <Alert
      severity="error"
      role="alert"
      aria-live="assertive"
      aria-atomic="true"
      sx={{ mb: 3 }}
    >
      <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
        Please fix the following errors:
      </Typography>
      <List dense disablePadding>
        {activeErrors.map(([field, message]) => (
          <ListItem key={field} disablePadding sx={{ py: 0.25 }}>
            {/* component="button" is keyboard-accessible — Enter activates (WCAG 2.1.1) */}
            <Link
              component="button"
              variant="body2"
              color="error.dark"
              underline="always"
              onClick={() => scrollAndFocus(fieldIds[field] ?? field)}
              sx={{ textAlign: 'left', cursor: 'pointer' }}
            >
              {message}
            </Link>
          </ListItem>
        ))}
      </List>
    </Alert>
  );
}
