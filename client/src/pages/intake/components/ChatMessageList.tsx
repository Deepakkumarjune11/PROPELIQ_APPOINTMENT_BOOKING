// Scrollable chat message list — auto-scrolls to the latest message after each update.
// `useRef` + `useEffect` pattern ensures the bottom is always visible after AI replies.
import Box from '@mui/material/Box';
import { useEffect, useRef } from 'react';
import type { ChatMessage } from '@/api/intakeChat';
import AiTypingIndicator from './AiTypingIndicator';
import ChatMessageItem from './ChatMessageItem';

interface ChatMessageListProps {
  messages: ChatMessage[];
  isTyping: boolean;
  patientInitial?: string;
}

export default function ChatMessageList({ messages, isTyping, patientInitial }: ChatMessageListProps) {
  const bottomRef = useRef<HTMLDivElement>(null);

  // Scroll to bottom whenever a new message is appended or typing state changes
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isTyping]);

  return (
    <Box
      sx={{
        flex: 1,
        overflowY: 'auto',
        px: { xs: 2, sm: 3 },
        py: 2,
        display: 'flex',
        flexDirection: 'column',
        gap: 2,
      }}
      role="log"
      aria-live="polite"
      aria-label="Conversation"
    >
      {messages.map((msg, idx) => (
        <ChatMessageItem
          key={`${msg.role}-${idx}`}
          message={msg}
          patientInitial={patientInitial}
        />
      ))}

      {isTyping && <AiTypingIndicator />}

      {/* Scroll anchor */}
      <div ref={bottomRef} aria-hidden="true" />
    </Box>
  );
}
