import ChatWidget from "./chat/ChatWidget";
import styles from "./App.module.css";

export default function App() {
  return (
    <div className={styles.shell}>
      <header className={styles.masthead}>
        <div className={styles.brand}>
          <p className={styles.brandTitle}>Bostadskö-assistenten</p>
          <span className={styles.brandLabel}>Digital vägledning</span>
        </div>
      </header>

      <main className={styles.content}>
        <section className={styles.intro} aria-labelledby="intro-heading">
          <h1 id="intro-heading" className={styles.introTitle}>
            Fråga om bostadskön
          </h1>
          <p className={styles.introText}>
            Få hjälp med köregler, ansökan och vanliga frågor. Assistenten
            ger vägledning — aktuella regler hos din bostadsförmedlare gäller
            alltid.
          </p>
        </section>

        <div className={styles.panelSlot}>
          <ChatWidget />
        </div>
      </main>
    </div>
  );
}
