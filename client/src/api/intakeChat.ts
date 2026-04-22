// API client for POST /api/v1/patients/{patientId}/intake/chat.
// No LLM calls from the frontend — the AI service is entirely encapsulated in the backend.
const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? '';

/** A single turn in the conversation — used for display and for sending history to the backend. */
export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

/** Request body for one conversational turn. */
export interface SendChatMessageRequest {
  /** The text the patient just typed. Empty string on the very first call to trigger the AI greeting. */
  message: string;
  /** Full prior conversation so the backend can maintain context without server-side session state. */
  conversationHistory: Array<{ role: string; content: string }>;
}

/** Response from one conversational turn. */
export interface SendChatMessageResponse {
  /** AI-generated reply text to render in the chat window. */
  assistantMessage: string;
  /**
   * `true` when the AI has collected all required information.
   * `structuredAnswers` is present alongside this flag.
   */
  isComplete: boolean;
  /**
   * `true` when the AI service circuit-breaker has tripped (AC-5 / AIR-O02).
   * The UI should show the fallback banner and redirect to the manual form.
   */
  fallbackToManual: boolean;
  /**
   * Populated when `isComplete = true`. Keys are INTAKE_QUESTIONS ids so the manual form
   * pre-populates correctly if the patient switches modes (AC-3).
   */
  structuredAnswers?: Record<string, string>;
}

export async function sendIntakeChatMessage(
  patientId: string,
  payload: SendChatMessageRequest,
): Promise<SendChatMessageResponse> {
  const response = await fetch(
    `${BASE_URL}/api/v1/patients/${encodeURIComponent(patientId)}/intake/chat`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    },
  );

  if (!response.ok) {
    throw new Error(`Intake chat request failed with status ${response.status}`);
  }

  return response.json() as Promise<SendChatMessageResponse>;
}
