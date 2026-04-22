// Single chat message card — differentiates AI (left-aligned, white card) from patient (right-aligned, primary fill).
// Matches wireframe layout: AI has primary.500 avatar badge, patient has secondary avatar.
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import type { ChatMessage } from '@/api/intakeChat';

interface ChatMessageItemProps {
  message: ChatMessage;
  /** Patient's initial for their avatar. Falls back to "P". */
  patientInitial?: string;
}

export default function ChatMessageItem({ message, patientInitial = 'P' }: ChatMessageItemProps) {
  const isAi = message.role === 'assistant';

  const formattedTime = new Intl.DateTimeFormat('default', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(message.timestamp);

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: isAi ? 'row' : 'row-reverse',
        alignItems: 'flex-end',
        gap: 1,
        maxWidth: '85%',
        alignSelf: isAi ? 'flex-start' : 'flex-end',
      }}
    >
      {/* Avatar */}
      <Avatar
        sx={{
          width: 36,
          height: 36,
          bgcolor: isAi ? 'primary.main' : 'secondary.main',
          fontSize: 14,
          flexShrink: 0,
        }}
        aria-label={isAi ? 'AI Assistant' : 'You'}
      >
        {isAi ? 'AI' : patientInitial}
      </Avatar>

      {/* Message card */}
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: isAi ? 'flex-start' : 'flex-end', gap: 0.5 }}>
        <Card
          elevation={isAi ? 1 : 0}
          sx={{
            bgcolor: isAi ? 'background.paper' : 'primary.main',
            borderRadius: isAi ? '0 8px 8px 8px' : '8px 0 8px 8px',
          }}
        >
          <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
            <Typography
              variant="body2"
              sx={{ color: isAi ? 'text.primary' : '#fff', lineHeight: 1.5 }}
            >
              {message.content}
            </Typography>
          </CardContent>
        </Card>
        <Typography variant="caption" color="text.disabled" sx={{ px: 0.5 }}>
          {formattedTime}
        </Typography>
      </Box>
    </Box>
  );
}
