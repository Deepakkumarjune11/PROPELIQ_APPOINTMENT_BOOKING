// Renders a single intake question as a MUI form field.
// Reads initial value from intake-store (AC-3 — pre-populates on return from conversational mode).
// Writes changes back to intake-store via setAnswer so answers are shared across modes (AC-2).
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import Box from '@mui/material/Box';
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import InputAdornment from '@mui/material/InputAdornment';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import { useState } from 'react';

import type { IntakeQuestion } from '@/config/intakeQuestions';
import { useIntakeStore } from '@/stores/intake-store';

// Character counter becomes visible when value reaches 80% of maxLength
const COUNTER_THRESHOLD = 0.8;

interface IntakeQuestionFieldProps {
  question: IntakeQuestion;
}

export default function IntakeQuestionField({ question }: IntakeQuestionFieldProps) {
  const { answers, setAnswer } = useIntakeStore();
  const value = answers[question.id] ?? '';
  const [touched, setTouched] = useState(false);

  const hasError = touched && question.required && value.trim() === '';
  const showCounter =
    question.maxLength !== undefined &&
    value.length >= question.maxLength * COUNTER_THRESHOLD;

  const helperText = hasError
    ? `${question.label} is required.`
    : showCounter
    ? `${value.length}/${question.maxLength} characters`
    : undefined;

  const labelNode = question.tooltip ? (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
      {question.label}
      {question.required && ' *'}
      <Tooltip title={question.tooltip} placement="right" arrow>
        <InfoOutlinedIcon sx={{ fontSize: 16, color: 'text.secondary', cursor: 'help' }} />
      </Tooltip>
    </Box>
  ) : undefined;

  if (question.type === 'checkbox') {
    return (
      <FormControlLabel
        control={
          <Checkbox
            checked={value === 'true'}
            onChange={(e) => setAnswer(question.id, e.target.checked ? 'true' : 'false')}
          />
        }
        label={question.label}
      />
    );
  }

  if (question.type === 'select' && question.options) {
    return (
      <TextField
        id={question.id}
        select
        fullWidth
        label={labelNode ?? question.label}
        value={value}
        onChange={(e) => setAnswer(question.id, e.target.value)}
        onBlur={() => setTouched(true)}
        required={question.required}
        error={hasError}
        helperText={helperText}
        InputLabelProps={labelNode ? { shrink: true } : undefined}
      >
        {question.options.map((opt) => (
          <MenuItem key={opt} value={opt}>
            {opt}
          </MenuItem>
        ))}
      </TextField>
    );
  }

  // 'text' and 'multiline' both render as TextField
  const isMultiline = question.type === 'multiline';

  return (
    <TextField
      id={question.id}
      fullWidth
      label={labelNode ?? question.label}
      value={value}
      onChange={(e) => setAnswer(question.id, e.target.value)}
      onBlur={() => setTouched(true)}
      required={question.required}
      error={hasError}
      helperText={helperText}
      multiline={isMultiline}
      minRows={isMultiline ? 3 : undefined}
      inputProps={question.maxLength ? { maxLength: question.maxLength, 'aria-required': question.required } : { 'aria-required': question.required }}
      InputProps={
        question.tooltip && !isMultiline
          ? {
              endAdornment: (
                <InputAdornment position="end">
                  <Tooltip title={question.tooltip} placement="left" arrow>
                    <InfoOutlinedIcon sx={{ fontSize: 18, color: 'text.secondary', cursor: 'help' }} />
                  </Tooltip>
                </InputAdornment>
              ),
            }
          : undefined
      }
    />
  );
}
