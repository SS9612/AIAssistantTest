import { useCallback, useRef, useState } from "react";
import { ChatApiClientError, sendChatMessage } from "../api/chatApi";
import type { ChatMessage, ChatRequestState } from "./chat.types";

const MAX_LENGTH = 2000;

function createId(): string {
  return crypto.randomUUID();
}

function validateDraft(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return "Skriv en fråga innan du skickar.";
  }
  if (trimmed.length > MAX_LENGTH) {
    return "Meddelandet får vara högst 2000 tecken.";
  }
  return null;
}

export function useChat() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState("");
  const [status, setStatus] = useState<ChatRequestState>("idle");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [requestError, setRequestError] = useState<string | null>(null);
  const lastFailedMessageRef = useRef<string | null>(null);
  const sendingRef = useRef(false);

  const requestAssistantReply = useCallback(async (message: string) => {
    try {
      const response = await sendChatMessage(message);
      setMessages((current) => [
        ...current,
        {
          id: createId(),
          role: "assistant",
          content: response.reply,
        },
      ]);
      lastFailedMessageRef.current = null;
      setRequestError(null);
      setStatus("idle");
    } catch (error) {
      const chatError =
        error instanceof ChatApiClientError
          ? error
          : new ChatApiClientError(
              "unexpected",
              "Något gick fel hos assistenten. Försök igen om en stund.",
            );

      if (chatError.kind === "validation") {
        setFieldError(chatError.message);
        setStatus("idle");
        return;
      }

      lastFailedMessageRef.current = message;
      setRequestError(chatError.message);
      setStatus("error");
    }
  }, []);

  const sendMessage = useCallback(
    async (rawMessage?: string) => {
      if (sendingRef.current) {
        return;
      }

      const source = rawMessage ?? draft;
      const validationError = validateDraft(source);
      if (validationError) {
        setFieldError(validationError);
        setRequestError(null);
        return;
      }

      const message = source.trim();
      sendingRef.current = true;
      setFieldError(null);
      setRequestError(null);
      setStatus("sending");
      setDraft("");

      setMessages((current) => [
        ...current,
        {
          id: createId(),
          role: "user",
          content: message,
        },
      ]);

      try {
        await requestAssistantReply(message);
      } finally {
        sendingRef.current = false;
      }
    },
    [draft, requestAssistantReply],
  );

  const retry = useCallback(async () => {
    const failedMessage = lastFailedMessageRef.current;
    if (!failedMessage || sendingRef.current) {
      return;
    }

    sendingRef.current = true;
    setFieldError(null);
    setRequestError(null);
    setStatus("sending");

    try {
      await requestAssistantReply(failedMessage);
    } finally {
      sendingRef.current = false;
    }
  }, [requestAssistantReply]);

  const updateDraft = useCallback((value: string) => {
    setDraft(value);
    setFieldError(null);
  }, []);

  return {
    messages,
    draft,
    status,
    fieldError,
    requestError,
    setDraft: updateDraft,
    sendMessage,
    retry,
  };
}
