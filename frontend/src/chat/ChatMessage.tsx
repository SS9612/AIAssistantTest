import styles from "./ChatWidget.module.css";
import type { ChatMessage as ChatMessageModel } from "./chat.types";

interface ChatMessageProps {
  message: ChatMessageModel;
}

export default function ChatMessage({ message }: ChatMessageProps) {
  const isUser = message.role === "user";

  return (
    <div
      className={`${styles.row} ${isUser ? styles.rowUser : styles.rowAssistant}`}
    >
      <div
        className={`${styles.bubble} ${isUser ? styles.bubbleUser : styles.bubbleAssistant}`}
      >
        {message.content}
      </div>
    </div>
  );
}
