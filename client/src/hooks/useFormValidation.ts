import { useCallback, useState } from 'react';

/**
 * Synchronous field validator. Returns an empty string for valid values,
 * or a descriptive error message string for invalid values.
 *
 * Synchronous execution on blur satisfies the AC-1 "within 300ms" threshold
 * because MUI `onBlur` fires in the same browser event-loop tick as focus loss —
 * no artificial setTimeout delay is introduced anywhere in the validation path.
 */
export type FieldValidator<V = string> = (value: V) => string;

/** Maps each field key in form type T to an optional validator function. */
export type ValidationSchema<T extends object> = {
  [K in keyof T]?: FieldValidator<T[K]>;
};

interface UseFormValidationReturn<T extends object> {
  /** Active error messages keyed by field name. Only fields with messages are present. */
  errors: Partial<Record<keyof T, string>>;
  /** Tracks which fields the user has interacted with (blurred at least once). */
  touched: Partial<Record<keyof T, boolean>>;
  /**
   * Call from `onBlur` on each field. Marks field as touched and runs its validator.
   * Execution is synchronous — error appears in the same render cycle as blur (AC-1 ≤300ms).
   */
  handleBlur: (field: keyof T, value: T[keyof T]) => void;
  /**
   * Merges server-returned field errors into local error state.
   * Server errors are authoritative — they override any existing client-side error
   * for the same field (edge case from AC specification).
   * Also marks all server-errored fields as touched so error messages render immediately.
   */
  handleServerErrors: (serverErrors: Partial<Record<keyof T, string>>) => void;
  /**
   * Validates all fields regardless of touched state (call on form submit).
   * Sets all errors and returns whether the form is currently valid.
   * Use the returned boolean to gate form submission (AC-2 trigger for FormErrorSummary).
   */
  validate: (values: T) => boolean;
  /** Clears the error for a single field (call on `onChange` to clear stale errors). */
  clearError: (field: keyof T) => void;
  /** `true` when no error messages are present across all fields. */
  isValid: boolean;
}

/**
 * Generic, field-agnostic form validation hook (US_039 AC-1, AC-2, AC-3).
 *
 * @example
 * ```tsx
 * const { errors, touched, handleBlur, validate, clearError } =
 *   useFormValidation<{ email: string; password: string }>({
 *     email: (v) => (!v ? 'Email is required' : ''),
 *     password: (v) => (!v ? 'Password is required' : ''),
 *   });
 * ```
 */
export function useFormValidation<T extends object>(
  schema: ValidationSchema<T>,
): UseFormValidationReturn<T> {
  const [errors, setErrors] = useState<Partial<Record<keyof T, string>>>({});
  const [touched, setTouched] = useState<Partial<Record<keyof T, boolean>>>({});

  const handleBlur = useCallback(
    (field: keyof T, value: T[keyof T]) => {
      setTouched((prev) => ({ ...prev, [field]: true }));
      const message = schema[field]?.(value) ?? '';
      setErrors((prev) => ({ ...prev, [field]: message }));
    },
    [schema],
  );

  const handleServerErrors = useCallback(
    (serverErrors: Partial<Record<keyof T, string>>) => {
      // Server errors override any existing client error for the same field
      setErrors((prev) => ({ ...prev, ...serverErrors }));
      // Mark server-errored fields as touched so error messages render
      const newTouched = Object.fromEntries(
        Object.keys(serverErrors).map((k) => [k, true]),
      ) as Partial<Record<keyof T, boolean>>;
      setTouched((prev) => ({ ...prev, ...newTouched }));
    },
    [],
  );

  const validate = useCallback(
    (values: T): boolean => {
      const newErrors: Partial<Record<keyof T, string>> = {};
      const newTouched: Partial<Record<keyof T, boolean>> = {};

      for (const field of Object.keys(schema) as (keyof T)[]) {
        const message = schema[field]?.(values[field]) ?? '';
        newErrors[field] = message;
        newTouched[field] = true;
      }

      setErrors(newErrors);
      setTouched(newTouched);

      return Object.values(newErrors).every((e) => !e);
    },
    [schema],
  );

  const clearError = useCallback((field: keyof T) => {
    setErrors((prev) => ({ ...prev, [field]: '' }));
  }, []);

  const isValid = Object.values(errors).every((e) => !e);

  return { errors, touched, handleBlur, handleServerErrors, validate, clearError, isValid };
}
