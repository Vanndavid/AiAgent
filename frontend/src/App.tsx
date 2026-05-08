import { useEffect, useState } from 'react'
import './App.css'

type StackStatus = {
  api: boolean
  postgres: boolean
  ragApi: boolean
}

function App() {
  const [status, setStatus] = useState<StackStatus | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    fetch('/api/stack-status')
      .then(async (r) => {
        if (!r.ok) {
          throw new Error(`HTTP ${r.status}`)
        }
        return r.json() as Promise<StackStatus>
      })
      .then((body) => {
        if (!cancelled) {
          setStatus(body)
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Request failed')
        }
      })
    return () => {
      cancelled = true
    }
  }, [])

  const pill = (ok?: boolean) => (
    <span className={`pill ${ok ? 'ok' : 'bad'}`}>{ok ? 'connected' : 'down'}</span>
  )

  return (
    <main className="shell">
      <header className="header">
        <h1>Job Assistant</h1>
        <p className="subtitle">Development connectivity • SaaS foundation</p>
      </header>

      <section className="card">
        <h2>Stack status</h2>
        <p className="hint">
          Start infra with <code>docker compose up -d</code>, then{' '}
          <code>dotnet run --project backend/JobAssistant.Api</code> from the repo root.
        </p>

        {error && <p className="error">Could not reach API: {error}</p>}

        {status && (
          <ul className="rows">
            <li>
              ASP.NET API <span className="mono">/api/stack-status</span> {pill(status.api)}
            </li>
            <li>
              PostgreSQL {pill(status.postgres)}
            </li>
            <li>
              RAG service (FastAPI + FAISS) {pill(status.ragApi)}
            </li>
          </ul>
        )}

        {!status && !error && <p className="muted">Loading…</p>}
      </section>
    </main>
  )
}

export default App
