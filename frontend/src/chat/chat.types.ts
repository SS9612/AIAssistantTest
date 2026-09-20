export type ChatRole = "user" | "assistant";

export type ChatRequestState = "idle" | "sending" | "error";

export interface ChatMessage {
  id: string;
  role: ChatRole;
  content: string;
}

export interface ChatApiRequest {
  message: string;
}

export interface ChatApiResponse {
  reply: string;
}

export type ChatErrorKind = "validation" | "network" | "unexpected";

export interface ChatError {
  kind: ChatErrorKind;
  message: string;
}
