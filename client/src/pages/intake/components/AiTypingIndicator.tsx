// Three-dot typing animation shown while the AI response is pending (AC-2).
// Rendered in an AI-style card to visually match incoming messages.
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';

/** Keyframe string injected via MUI sx — bounce animation for each dot. */
const dotSx = (delay: string) => ({
  width: 8,
  height: 8,
  borderRadius: '50%',
  bgcolor: 'text.disabled',
  display: 'inline-block',
  animation: 'bounce 1.2s ease-in-out infinite',
  animationDelay: delay,
  '@keyframes bounce': {
    '0%, 80%, 100%': { transform: 'scale(0.6)', opacity: 0.4 },
    '40%': { transform: 'scale(1)', opacity: 1 },
  },
});

export default function AiTypingIndicator() {
  return (
    <Box
      sx={{ display: 'flex', flexDirection: 'row', alignItems: 'flex-end', gap: 1, alignSelf: 'flex-start' }}
      aria-label="AI assistant is typing"
      role="status"
    >
      <Avatar
        sx={{ width: 36, height: 36, bgcolor: 'primary.main', fontSize: 14, flexShrink: 0 }}
        aria-hidden="true"
      >
        AI
      </Avatar>
      <Card elevation={1} sx={{ borderRadius: '0 8px 8px 8px' }}>
        <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
          <Box sx={{ display: 'flex', gap: 0.75, alignItems: 'center', height: 20 }}>
            <Box sx={dotSx('0s')} />
            <Box sx={dotSx('0.2s')} />
            <Box sx={dotSx('0.4s')} />
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
