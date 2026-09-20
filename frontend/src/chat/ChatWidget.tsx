import { useEffect, useRef } from "react";
import ChatComposer from "./ChatComposer";
import ChatMessage from "./ChatMessage";
import styles from "./ChatWidget.module.css";
import { useChat } from "./useChat";

const SUGGESTED_PROMPTS = [
  "Hur fungerar bostadskön?",
  "Vad krävs för att söka en bostad?",
  "Hur uppdaterar jag mina uppgifter?",
] as const;

export default function ChatWidget() {
  const {
    messages,
    draft,
    status,
    fieldError,
    requestError,
    setDraft,
    sendMessage,
    retry,
  } = useChat();

  const endRef = useRef<HTMLDivElement>(null);
  const isSending = status === "sending";

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages, isSending, requestError]);

  return (
    <section className={styles.widget} aria-label="Bostadskö-assistenten">
      <header className={styles.header}>
        <div className={styles.mark} aria-hidden="true">
          AI
        </div>
        <div className={styles.headerText}>
          <h2 className={styles.title}>Bostadskö-assistenten</h2>
          <span className={styles.status}>
            <span className={styles.statusDot} aria-hidden="true" />
            Tillgänglig
          </span>
        </div>
      </header>

      <div
        className={styles.messages}
        role="log"
        aria-live="polite"
        aria-relevant="additions"
        aria-busy={isSending}
      >
        {messages.length === 0 ? (
          <div className={styles.welcome}>
            <p className={styles.welcomeText}>
              Hej! Jag hjälper dig med frågor om bostadskön, ansökan och
              vanliga regler. Välj en snabbfråga eller skriv din egen.
            </p>
            <div className={styles.suggestions}>
              {SUGGESTED_PROMPTS.map((prompt) => (
                <button
                  key={prompt}
                  type="button"
                  className={styles.suggestion}
                  disabled={isSending}
                  onClick={() => void sendMessage(prompt)}
                >
                  {prompt}
                </button>
              ))}
            </div>
          </div>
        ) : null}

        {messages.map((message) => (
          <ChatMessage key={message.id} message={message} />
        ))}

        {isSending ? (
          <div className={`${styles.row} ${styles.rowAssistant}`}>
            <div
              className={`${styles.bubble} ${styles.bubbleAssistant}`}
              aria-label="Assistenten skriver"
            >
              <span className={styles.typing} aria-hidden="true">
                <span className={styles.typingDot} />
                <span className={styles.typingDot} />
                <span className={styles.typingDot} />
              </span>
            </div>
          </div>
        ) : null}

        <div ref={endRef} />
      </div>

      {requestError ? (
        <div className={styles.alert} role="alert">
          <span>{requestError}</span>
          <button type="button" className={styles.retry} onClick={() => void retry()}>
            Försök igen
          </button>
        </div>
      ) : null}

      <ChatComposer
        value={draft}
        disabled={isSending}
        fieldError={fieldError}
        onChange={setDraft}
        onSubmit={() => void sendMessage()}
      />
    </section>
  );
}
