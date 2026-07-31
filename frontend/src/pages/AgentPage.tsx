import { useState } from 'react'
import '../App.css'

type AgentRunResult = {
  goal: string
  final_answer: string
  scratchpad: string[]
  tools_used?: string[]
}

export default function AgentPage() {
  const [goal, setGoal] = useState(
    'List my job applications and retrieve similar backend engineer roles from RAG.',
  )
  const [result, setResult] = useState<AgentRunResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [running, setRunning] = useState(false)

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
      setResult(body as AgentRunResult)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to run agent')
    } finally {
      setRunning(false)
    }
  }

  return (
    <section className="card">
      <div className="card-header">
        <h2>AI agent</h2>
      </div>
      <p className="hint">
        Calls the ReAct loop via <code>POST /api/agent/run</code>. The agent can list/create
        applications (API), retrieve/ingest job chunks (RAG), or research/save notes. Uses a
        deterministic fake LLM locally so you can verify wiring without API keys.
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
  )
}
