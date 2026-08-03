import { useCallback, useEffect, useState } from 'react'
import '../App.css'

type AgentRunResult = {
  id?: string | null
  goal: string
  final_answer: string
  scratchpad: string[]
  tools_used?: string[]
  created_at?: string | null
}

export default function AgentPage() {
  const [goal, setGoal] = useState(
    'List my job applications and retrieve similar backend engineer roles from RAG.',
  )
  const [result, setResult] = useState<AgentRunResult | null>(null)
  const [history, setHistory] = useState<AgentRunResult[]>([])
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [running, setRunning] = useState(false)
  const [expandedId, setExpandedId] = useState<string | null>(null)

  const loadHistory = useCallback(async () => {
    try {
      const response = await fetch('/api/agent/runs?limit=20')
      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { error?: string } | null
        throw new Error(body?.error ?? `HTTP ${response.status}`)
      }
      setHistory((await response.json()) as AgentRunResult[])
      setHistoryError(null)
    } catch (err: unknown) {
      setHistoryError(err instanceof Error ? err.message : 'Failed to load run history')
    }
  }, [])

  useEffect(() => {
    let cancelled = false

    fetch('/api/agent/runs?limit=20')
      .then(async (response) => {
        if (!response.ok) {
          const body = (await response.json().catch(() => null)) as { error?: string } | null
          throw new Error(body?.error ?? `HTTP ${response.status}`)
        }
        return response.json() as Promise<AgentRunResult[]>
      })
      .then((body) => {
        if (!cancelled) {
          setHistory(body)
          setHistoryError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setHistoryError(err instanceof Error ? err.message : 'Failed to load run history')
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setRunning(true)
    setError(null)
    setResult(null)
    try {
      const response = await fetch('/api/agent/run', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ goal }),
      })
      const body = (await response.json().catch(() => null)) as
        | AgentRunResult
        | { error?: string; detail?: string }
        | null
      if (!response.ok) {
        const err = body && 'error' in body ? body.error : `HTTP ${response.status}`
        const detail = body && 'detail' in body && body.detail ? `: ${body.detail}` : ''
        throw new Error(`${err ?? 'Request failed'}${detail}`)
      }
      const run = body as AgentRunResult
      setResult(run)
      if (run.id) {
        setHistory((prev) => [run, ...prev.filter((r) => r.id !== run.id)])
        setExpandedId(run.id)
      } else {
        void loadHistory()
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to run agent')
    } finally {
      setRunning(false)
    }
  }

  return (
    <>
      <section className="card">
        <div className="card-header">
          <h2>AI agent</h2>
        </div>
        <p className="hint">
          Calls the ReAct loop via <code>POST /api/agent/run</code>. Successful runs are stored in
          Postgres and listed below. Uses a deterministic fake LLM locally so you can verify wiring
          without API keys.
        </p>

        <form className="form" onSubmit={(e) => void onSubmit(e)}>
          <label className="full-width">
            Goal
            <textarea
              value={goal}
              onChange={(e) => setGoal(e.target.value)}
              rows={4}
              required
              disabled={running}
            />
          </label>
          <div className="form-actions">
            <button type="submit" className="btn primary" disabled={running || !goal.trim()}>
              {running ? 'Running…' : 'Run agent'}
            </button>
          </div>
        </form>

        {error && <p className="error">{error}</p>}

        {result && (
          <div className="agent-result">
            <h3>Final answer</h3>
            <pre className="agent-trace">{result.final_answer}</pre>
            {result.tools_used && result.tools_used.length > 0 && (
              <>
                <h3>Tools used</h3>
                <p className="hint">{result.tools_used.join(' → ')}</p>
              </>
            )}
            <h3>Agent trace</h3>
            <pre className="agent-trace">{result.scratchpad.join('\n')}</pre>
          </div>
        )}
      </section>

      <section className="card">
        <div className="card-header">
          <h2>Recent runs</h2>
          <button type="button" className="btn secondary" onClick={() => void loadHistory()}>
            Refresh
          </button>
        </div>
        {historyError && <p className="error">{historyError}</p>}
        {!historyError && history.length === 0 && (
          <p className="muted">No saved runs yet. Run the agent above (Postgres required).</p>
        )}
        {history.length > 0 && (
          <ul className="run-list">
            {history.map((run) => {
              const key = run.id ?? `${run.created_at}-${run.goal}`
              const open = expandedId === run.id
              return (
                <li key={key} className="run-item">
                  <button
                    type="button"
                    className="run-summary"
                    onClick={() => setExpandedId(open ? null : (run.id ?? null))}
                  >
                    <span className="run-goal">{run.goal}</span>
                    <span className="mono">
                      {run.created_at
                        ? new Date(run.created_at).toLocaleString()
                        : 'unsaved'}
                    </span>
                  </button>
                  {run.tools_used && run.tools_used.length > 0 && (
                    <p className="hint run-tools">{run.tools_used.join(' → ')}</p>
                  )}
                  {open && (
                    <div className="agent-result">
                      <pre className="agent-trace">{run.final_answer}</pre>
                      <pre className="agent-trace">{run.scratchpad.join('\n')}</pre>
                    </div>
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </section>
    </>
  )
}
