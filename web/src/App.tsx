export default function App() {
  const scrollToDownload = () => {
    document.getElementById('download')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  return (
    <main className="page">
      <div className="page__grid" aria-hidden="true" />

      <section className="hero">
        <div className="hero__grid">
          <div className="hero__content">
           
            <div className="status-pill">
              <span className="status-pill__dot" />
              Open-source Windows network utility
            </div>
            
            <h1>Prioritize the right adapter. Measure real speed. Stay in control.</h1>
            <p className="lede">
              Buff is a WinUI 3 desktop app that lets you set preferred network routes, monitor adapter health, and run built-in M-Lab
              speed tests in one focused workspace.
            </p>

            <div className="hero__highlights" aria-label="Buff highlights">
              <span>Preferred adapter routing</span>
              <span>Live network visibility</span>
              <span>Native M-Lab NDT7 tests</span>
            </div>

            <div className="actions">
              <button className="button button--primary" type="button" onClick={scrollToDownload}>
                Download UI
              </button>
              <a className="button button--secondary" href="https://github.com/shanto-joseph/Buff" target="_blank" rel="noreferrer">
                View GitHub repo
              </a>
            </div>

            <div className="hero__microstats" aria-label="Buff platform details">
              <span>Windows 10 / 11</span>
              <span>WinUI 3 desktop app</span>
              <span>Open source</span>
            </div>
          </div>
          
          <div className="hero__visual">
            <img src="https://res.cloudinary.com/dk6i1ld2q/image/upload/v1776518011/img_lyi7gr.png" alt="Buff app interface" className="hero__image" />
          </div>
        </div>
      </section>

      <section className="detail-band" id="details">
        <article className="detail-band__item">
          <p className="panel__label">01</p>
          <h2>Route priority</h2>
          <p>Pick the adapter Windows should trust first and keep routing behavior predictable.</p>
        </article>

        <article className="detail-band__item">
          <p className="panel__label">02</p>
          <h2>Real-time visibility</h2>
          <p>Track adapter status, connection shifts, and route quality as your setup changes.</p>
        </article>

        <article className="detail-band__item" id="download">
          <p className="panel__label">03</p>
          <h2>Download zone</h2>
          <p>Keep the download call-to-action inside the page, with GitHub linked separately for project details.</p>
        </article>
      </section>

      <footer className="footer">
        <div className="footer-panel">
          <div className="footer-panel__wordmark" aria-hidden="true">Buff</div>

          <div className="footer-panel__top">
            <div className="footer-brand">
              <img src="/buff.png" alt="" className="footer-brand__icon" aria-hidden="true" />
              <div>
                <p className="footer-brand__name">Buff</p>
                <p className="footer-brand__tag">Network Manager for Windows</p>
              </div>
            </div>

            <div className="footer-links">
              <a href="#details">Features</a>
              <a href="https://github.com/shanto-joseph/Buff" target="_blank" rel="noreferrer">GitHub</a>
              <a href="https://github.com/shanto-joseph/Buff/releases/latest" target="_blank" rel="noreferrer">Releases</a>
              <a href="https://coffee.shantojoseph.com" target="_blank" rel="noreferrer" className="footer-links__coffee">Buy me a coffee</a>
            </div>
          </div>

          <div className="soft-divider" />

          <div className="footer-panel__bottom">
            <p>© 2026 Buff. Open source.</p>
            <p>Windows 10 & 11 support</p>
          </div>
        </div>
      </footer>

      <a
        className="coffee-badge"
        href="https://coffee.shantojoseph.com"
        target="_blank"
        rel="noreferrer"
        aria-label="Support Buff with coffee"
      >
        <span className="coffee-badge__dot" aria-hidden="true" />
        <span>Coffee</span>
        <span className="coffee-badge__popup">Support Buff</span>
      </a>
    </main>
  )
}
