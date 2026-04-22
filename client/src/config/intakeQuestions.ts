// Static intake question definitions for the manual intake form (SCR-004).
// Phase-1 fixed question set per YAGNI — upgrade path is to fetch from API when configurable questions are needed.

export interface IntakeQuestion {
  id: string;
  label: string;
  /** Rendering hint: multiline textarea, single-line text, MUI Select, or Checkbox group. */
  type: 'text' | 'multiline' | 'select' | 'checkbox';
  required: boolean;
  maxLength?: number;
  /** Populates a fixed option list for type="select". */
  options?: string[];
  /** UXR-003: inline guidance shown as MUI Tooltip on the label. */
  tooltip?: string;
}

export const INTAKE_QUESTIONS: IntakeQuestion[] = [
  {
    id: 'reasonForVisit',
    label: 'Reason for visit',
    type: 'multiline',
    required: true,
    maxLength: 500,
    tooltip: 'Describe your main concern or reason for this appointment.',
  },
  {
    id: 'chiefComplaint',
    label: 'Chief complaint',
    type: 'multiline',
    required: true,
    maxLength: 500,
  },
  {
    id: 'currentMeds',
    label: 'Current medications',
    type: 'multiline',
    required: false,
    maxLength: 500,
    tooltip: 'List name and dosage of all medications you currently take.',
  },
  {
    id: 'allergies',
    label: 'Known allergies',
    type: 'multiline',
    required: false,
    maxLength: 500,
  },
  {
    id: 'medicalHistory',
    label: 'Relevant medical history',
    type: 'multiline',
    required: false,
    maxLength: 500,
  },
];

/** Subset of question IDs that must be answered before submit is enabled. */
export const REQUIRED_QUESTION_IDS = INTAKE_QUESTIONS
  .filter((q) => q.required)
  .map((q) => q.id);
