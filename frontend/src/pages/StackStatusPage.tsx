import { useCallback, useEffect, useState } from 'react'
import type { StackStatus } from '../types/application'
import '../App.css'

async function fetchStackStatus(): Promise<StackStatus> {
  const response = await fetch('/api/stack-status')
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`)
  }
  return response.json() as Promise<StackStatus>
}

export default function StackStatusPage() {
  const [status, setStatus] = useState<StackStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setStatus(await fetchStackStatus())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Request failed')
      setStatus(null)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let cancelled = false

    fetchStackStatus()
      .then((body) => {
        if (!cancelled) {
          setStatus(body)
          setLoading(false)
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Request failed')
          setLoading(false)
        }
      })

    const interval = window.setInterval(() => {
      void fetchStackStatus()
        .then((body) => {
          if (!cancelled) {
            setStatus(body)
          }
        })
        .catch(() => {
          /* keep previous status during background refresh */
        })
    }, 30_000)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [])

  const pill = (ok?: boolean) => (
    <span className={`pill ${ok ? 'ok' : 'bad'}`}>{ok ? 'connected' : 'down'}</span>
  )

  return (
    <section className="card">
      <div className="card-header">
        <h2>Stack status</h2>
        <button type="button" className="btn secondary" onClick={() => void refresh()} disabled={loading}>
          {loading ? 'Refreshing…' : 'Refresh'}
        </button>
      </div>
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
            {!status.postgres && status.postgresError && (
              <span className="detail-error">{status.postgresError}</span>
            )}
          </li>
          <li>
            RAG service (FastAPI + FAISS) {pill(status.ragApi)}
            {!status.ragApi && status.ragError && (
              <span className="detail-error">{status.ragError}</span>
            )}
          </li>
          <li>
            AI agent (ReAct loop) {pill(status.aiAgent)}
            {!status.aiAgent && status.aiAgentError && (
              <span className="detail-error">{status.aiAgentError}</span>
            )}
          </li>
        </ul>
      )}

      {!status && !error && loading && <p className="muted">Loading…</p>}
    </section>
  )
}
