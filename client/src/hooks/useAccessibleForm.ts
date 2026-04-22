import { useCallback, useRef, useState } from 'react';

interface FieldRef {
  fieldId: string;
  ref: React.RefObject<HTMLInputElement>;
}

export interface UseAccessibleFormReturn {
  /** Current announcement message — wire to `<LiveRegion politeness="assertive" />`. */
  announcement: string;
  /**
   * Call on submit when validation fails.
   * Announces the error count to screen readers and moves focus to the first
   * field with an error (WCAG 3.3.1 Error Identification).
   * Pass field IDs in DOM/tab order so focus lands on the topmost error.
   */
  focusFirstError: (errorFieldIds: string[]) => void;
  /**
   * Register an input ref so `focusFirstError` can programmatically focus it.
   * Call once per field — safe to call in render or `useEffect`; duplicates ignored.
   */
  registerField: (fieldId: string, ref: React.RefObject<HTMLInputElement>) => void;
}

/**
 * Centralizes WCAG 3.3.1 (Error Identification) and 3.3.3 (Error Suggestion)
 * behaviors for all form pages (AC-4).
 *
 * Pattern:
 * 1. Call `registerField('email', emailRef)` for each field in `useEffect`.
 * 2. On submit failure, call `focusFirstError(['email', 'password'])` with
 *    the IDs of fields that have errors, in tab order.
 * 3. Render `<LiveRegion message={announcement} politeness="assertive" />`.
 */
export function useAccessibleForm(): UseAccessibleFormReturn {
  const [announcement, setAnnouncement] = useState('');
  const fieldRefs = useRef<FieldRef[]>([]);

  const registerField = useCallback(
    (fieldId: string, ref: React.RefObject<HTMLInputElement>) => {
      if (!fieldRefs.current.find((f) => f.fieldId === fieldId)) {
        fieldRefs.current.push({ fieldId, ref });
      }
    },
    [],
  );

  const focusFirstError = useCallback((errorFieldIds: string[]) => {
    if (errorFieldIds.length === 0) {
      setAnnouncement('');
      return;
    }

    const count = errorFieldIds.length;
    setAnnouncement(
      `Form submission failed. ${count} error${count === 1 ? '' : 's'} found. ` +
        'Please review and correct the highlighted fields.',
    );

    // Find the first registered field that has an error (preserves DOM order)
    const firstErrorField = fieldRefs.current.find((f) =>
      errorFieldIds.includes(f.fieldId),
    );

    if (firstErrorField?.ref.current) {
      // Defer focus by one tick so the assertive announcement fires before the
      // focus shift — prevents AT from reading the new field before the error count.
      setTimeout(() => {
        firstErrorField.ref.current?.focus();
      }, 100);
    }
  }, []);

  return { announcement, focusFirstError, registerField };
}
