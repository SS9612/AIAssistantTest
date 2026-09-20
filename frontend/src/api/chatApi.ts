import type { ChatApiRequest, ChatApiResponse, ChatError } from "../chat/chat.types";

export class ChatApiClientError extends Error {
  readonly kind: ChatError["kind"];

  constructor(kind: ChatError["kind"], message: string) {
    super(message);
    this.name = "ChatApiClientError";
    this.kind = kind;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readValidationMessage(payload: unknown): string | null {
  if (!isRecord(payload) || !isRecord(payload.errors)) {
    return null;
  }

  const messageErrors = payload.errors.Message ?? payload.errors.message;
  if (Array.isArray(messageErrors) && typeof messageErrors[0] === "string") {
    return "Meddelandet måste vara mellan 1 och 2000 tecken.";
  }

  return "Kontrollera din fråga och försök igen.";
}

export async function sendChatMessage(
  message: string,
): Promise<ChatApiResponse> {
  const body: ChatApiRequest = { message };

  let response: Response;
  try {
    response = await fetch("/api/chat", {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    });
  } catch {
    throw new ChatApiClientError(
      "network",
      "Kunde inte nå assistenten. Kontrollera att API:et körs och försök igen.",
    );
  }

  if (response.status === 400) {
    let payload: unknown = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    throw new ChatApiClientError(
      "validation",
      readValidationMessage(payload) ??
        "Kontrollera din fråga och försök igen.",
    );
  }

  if (!response.ok) {
    throw new ChatApiClientError(
      "unexpected",
      response.status === 502 || response.status === 503
        ? "Assistenten är tillfälligt otillgänglig. Försök igen om en stund."
        : "Något gick fel hos assistenten. Försök igen om en stund.",
    );
  }

  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    throw new ChatApiClientError(
      "unexpected",
      "Assistenten svarade med ett ogiltigt format.",
    );
  }

  if (!isRecord(payload) || typeof payload.reply !== "string") {
    throw new ChatApiClientError(
      "unexpected",
      "Assistenten svarade med ett ogiltigt format.",
    );
  }

  return { reply: payload.reply };
}
