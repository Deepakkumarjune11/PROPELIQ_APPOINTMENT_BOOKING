// Manages chat session state: message history, AI typing indicator, completion, and fallback.
// No LLM calls from FE — all intelligence is encapsulated in the backend (AIR-S04).
import { useMutation } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  sendIntakeChatMessage,
  type ChatMessage,
  type SendChatMessageResponse,
} from '@/api/intakeChat';
import { useIntakeStore } from '@/stores/intake-store';

export interface UseIntakeChatReturn {
  messages: ChatMessage[];
  isTyping: boolean;
  isComplete: boolean;
  isFallback: boolean;
  showInactivityWarning: boolean;
  send: (userMessage: string) => void;
}

/** Inactivity warning threshold — 4 minutes (240 000 ms). */
const INACTIVITY_WARNING_MS = 4 * 60 * 1000;

/** Auto-redirect delay after fallback banner appears (AC-5). */
const FALLBACK_REDIRECT_DELAY_MS = 2_000;

export function useIntakeChat(patientId: string): UseIntakeChatReturn {
  const navigate = useNavigate();
  const { mergeAnswers, setMode } = useIntakeStore();

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isTyping, setIsTyping] = useState(false);
  const [isComplete, setIsComplete] = useState(false);
  const [isFallback, setIsFallback] = useState(false);
  const [showInactivityWarning, setShowInactivityWarning] = useState(false);

  // Track the latest user activity timestamp for the inactivity timer
  const lastActivityRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const resetInactivityTimer = useCallback(() => {
    if (lastActivityRef.current) clearTimeout(lastActivityRef.current);
    setShowInactivityWarning(false);
    lastActivityRef.current = setTimeout(
      () => setShowInactivityWarning(true),
      INACTIVITY_WARNING_MS,
    );
  }, []);

  const handleApiSuccess = useCallback(
    (response: SendChatMessageResponse) => {
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: response.assistantMessage, timestamp: new Date() },
      ]);
      setIsTyping(false);

      if (response.fallbackToManual) {
        setIsFallback(true);
        setTimeout(() => {
          setMode('manual');
          navigate('/appointments/intake/manual');
        }, FALLBACK_REDIRECT_DELAY_MS);
        return;
      }

      if (response.isComplete && response.structuredAnswers) {
        // AC-3: merge structured answers into shared store so manual form pre-populates
        mergeAnswers(response.structuredAnswers);
        setIsComplete(true);
      }

      resetInactivityTimer();
    },
    [mergeAnswers, navigate, resetInactivityTimer, setMode],
  );

  const { mutate } = useMutation<
    SendChatMessageResponse,
    Error,
    string
  >({
    mutationFn: (userMessage: string) =>
      sendIntakeChatMessage(patientId, {
        message: userMessage,
        // Send the current conversation history (excluding the optimistically-appended user msg)
        conversationHistory: messages.map((m) => ({ role: m.role, content: m.content })),
      }),
    onSuccess: handleApiSuccess,
    onError: () => {
      setIsTyping(false);
      // Treat a network error as a fallback trigger (AC-5 defensive behaviour)
      setIsFallback(true);
      setTimeout(() => {
        setMode('manual');
        navigate('/appointments/intake/manual');
      }, FALLBACK_REDIRECT_DELAY_MS);
    },
  });

  const send = useCallback(
    (userMessage: string) => {
      if (isTyping || isComplete || isFallback) return;

      // Append user message optimistically
      if (userMessage.trim()) {
        setMessages((prev) => [
          ...prev,
          { role: 'user', content: userMessage.trim(), timestamp: new Date() },
        ]);
      }
      setIsTyping(true);
      resetInactivityTimer();
      mutate(userMessage.trim());
    },
    [isTyping, isComplete, isFallback, mutate, resetInactivityTimer],
  );

  // Fire the initial greeting on mount by sending an empty message (AC-1)
  useEffect(() => {
    setIsTyping(true);
    mutate('');
    resetInactivityTimer();
    return () => {
      if (lastActivityRef.current) clearTimeout(lastActivityRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { messages, isTyping, isComplete, isFallback, showInactivityWarning, send };
}
