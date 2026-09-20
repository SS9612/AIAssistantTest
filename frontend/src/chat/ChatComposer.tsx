import { useId, useRef } from "react";
import type { FormEvent, KeyboardEvent } from "react";
import styles from "./ChatWidget.module.css";

const MAX_LENGTH = 2000;

interface ChatComposerProps {
  value: string;
  disabled: boolean;
  fieldError: string | null;
  onChange: (value: string) => void;
  onSubmit: () => void;
}

export default function ChatComposer({
  value,
  disabled,
  fieldError,
  onChange,
  onSubmit,
}: ChatComposerProps) {
  const labelId = useId();
  const counterId = useId();
  const errorId = useId();
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const describedBy = fieldError
    ? `${counterId} ${errorId}`
    : counterId;

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    onSubmit();
    textareaRef.current?.focus();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      onSubmit();
    }
  }

  return (
    <form className={styles.composer} onSubmit={handleSubmit}>
      <label id={labelId} className={styles.label} htmlFor={`${labelId}-input`}>
        Din fråga
      </label>

      <div className={styles.inputRow}>
        <textarea
          ref={textareaRef}
          id={`${labelId}-input`}
          className={styles.textarea}
          value={value}
          maxLength={MAX_LENGTH}
          disabled={disabled}
          placeholder="Skriv din fråga om bostadskön..."
          aria-labelledby={labelId}
          aria-describedby={describedBy}
          aria-invalid={fieldError ? true : undefined}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={handleKeyDown}
        />
        <button className={styles.send} type="submit" disabled={disabled}>
          Skicka
        </button>
      </div>

      <div className={styles.meta}>
        <span id={counterId} className={styles.counter}>
          {value.length}/{MAX_LENGTH}
        </span>
        {fieldError ? (
          <span id={errorId} className={styles.fieldError} role="alert">
            {fieldError}
          </span>
        ) : null}
      </div>

      <p className={styles.disclaimer}>
        Svaren är vägledning. Aktuella regler hos din bostadsförmedlare gäller
        alltid.
      </p>
    </form>
  );
}
