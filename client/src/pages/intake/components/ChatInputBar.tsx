// Chat input bar — MUI TextField + circular Send button.
// Enter key submits (no newlines). Disabled when AI is typing or conversation is complete.
// Matches wireframe input area: pill-shaped TextField + 44px round send button (UXR-102).
import SendIcon from '@mui/icons-material/Send';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import { useRef, useState, type KeyboardEvent } from 'react';

interface ChatInputBarProps {
  disabled: boolean;
  onSend: (message: string) => void;
}

export default function ChatInputBar({ disabled, onSend }: ChatInputBarProps) {
  const [value, setValue] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  const handleSend = () => {
    const trimmed = value.trim();
    if (!trimmed || disabled) return;
    onSend(trimmed);
    setValue('');
    // Return focus to input after sending (UXR-101 keyboard flow)
    setTimeout(() => inputRef.current?.focus(), 0);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <Box
      sx={{
        backgroundColor: 'background.paper',
        boxShadow: '0 -1px 3px rgba(0,0,0,0.12)',
        px: { xs: 2, sm: 3 },
        py: 2,
        display: 'flex',
        gap: 1.5,
        alignItems: 'center',
      }}
      component="form"
      role="form"
      aria-label="Chat input"
      onSubmit={(e) => { e.preventDefault(); handleSend(); }}
    >
      <TextField
        inputRef={inputRef}
        fullWidth
        variant="outlined"
        placeholder="Type your response…"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        inputProps={{ maxLength: 1000, 'aria-label': 'Chat message' }}
        sx={{
          '& .MuiOutlinedInput-root': {
            borderRadius: '20px',
          },
        }}
        size="small"
      />
      <IconButton
        type="submit"
        disabled={disabled || !value.trim()}
        aria-label="Send message"
        sx={{
          width: 44,
          height: 44,
          bgcolor: 'primary.main',
          color: '#fff',
          flexShrink: 0,
          '&:hover': { bgcolor: 'primary.dark' },
          '&.Mui-disabled': { bgcolor: 'action.disabledBackground', color: 'action.disabled' },
        }}
      >
        <SendIcon fontSize="small" />
      </IconButton>
    </Box>
  );
}
