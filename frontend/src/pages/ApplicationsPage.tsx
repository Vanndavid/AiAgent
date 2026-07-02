import { type FormEvent, useCallback, useEffect, useState } from 'react'
import {
  APPLICATION_STATUSES,
  type ApplicationStatus,
  type CreateApplicationInput,
  type JobApplication,
  STATUS_LABELS,
} from '../types/application'
import '../App.css'

const emptyForm: CreateApplicationInput = {
  company: '',
  role: '',
  status: 'saved',
  appliedAt: '',
  notes: '',
}

async function fetchApplications(): Promise<JobApplication[]> {
  const response = await fetch('/api/applications')
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`)
  }
  return response.json() as Promise<JobApplication[]>
}

function formatDate(value: string | null) {
  if (!value) return '—'
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

export default function ApplicationsPage() {
  const [applications, setApplications] = useState<JobApplication[]>([])
  const [form, setForm] = useState<CreateApplicationInput>(emptyForm)
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setApplications(await fetchApplications())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to load applications')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let cancelled = false

    fetchApplications()
      .then((body) => {
        if (!cancelled) {
          setApplications(body)
          setLoading(false)
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Failed to load applications')
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (!form.company.trim() || !form.role.trim()) {
      setError('Company and role are required.')
      return
    }

    setSubmitting(true)
    setError(null)
    try {
      const payload = {
        company: form.company.trim(),
        role: form.role.trim(),
        status: form.status ?? 'saved',
        appliedAt: form.appliedAt ? new Date(form.appliedAt).toISOString() : null,
        notes: form.notes?.trim() || null,
      }

      const response = await fetch('/api/applications', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { error?: string } | null
        throw new Error(body?.error ?? `HTTP ${response.status}`)
      }

      setForm(emptyForm)
      setApplications(await fetchApplications())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to create application')
    } finally {
      setSubmitting(false)
    }
  }

  const handleStatusChange = async (id: string, status: ApplicationStatus) => {
    setError(null)
    try {
      const response = await fetch(`/api/applications/${id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status }),
      })
      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { error?: string } | null
        throw new Error(body?.error ?? `HTTP ${response.status}`)
      }
      setApplications(await fetchApplications())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to update status')
    }
  }

  const handleDelete = async (id: string) => {
    setError(null)
    try {
      const response = await fetch(`/api/applications/${id}`, { method: 'DELETE' })
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }
      setApplications(await fetchApplications())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to delete application')
    }
  }

  return (
    <>
      <section className="card">
        <h2>Add application</h2>
        <form className="form" onSubmit={(event) => void handleSubmit(event)}>
          <label>
            Company
            <input
              value={form.company}
              onChange={(e) => setForm((prev) => ({ ...prev, company: e.target.value }))}
              placeholder="Acme Corp"
              required
            />
          </label>
          <label>
            Role
            <input
              value={form.role}
              onChange={(e) => setForm((prev) => ({ ...prev, role: e.target.value }))}
              placeholder="Software Engineer"
              required
            />
          </label>
          <label>
            Status
            <select
              value={form.status ?? 'saved'}
              onChange={(e) =>
                setForm((prev) => ({ ...prev, status: e.target.value as ApplicationStatus }))
              }
            >
              {APPLICATION_STATUSES.map((status) => (
                <option key={status} value={status}>
                  {STATUS_LABELS[status]}
                </option>
              ))}
            </select>
          </label>
          <label>
            Applied date
            <input
              type="date"
              value={form.appliedAt ?? ''}
              onChange={(e) => setForm((prev) => ({ ...prev, appliedAt: e.target.value }))}
            />
          </label>
          <label className="full-width">
            Notes
            <textarea
              value={form.notes ?? ''}
              onChange={(e) => setForm((prev) => ({ ...prev, notes: e.target.value }))}
              rows={3}
              placeholder="Optional notes about this application"
            />
          </label>
          <div className="form-actions">
            <button type="submit" className="btn primary" disabled={submitting}>
              {submitting ? 'Saving…' : 'Add application'}
            </button>
          </div>
        </form>
      </section>

      <section className="card">
        <div className="card-header">
          <h2>Your applications</h2>
          <button type="button" className="btn secondary" onClick={() => void refresh()} disabled={loading}>
            Refresh
          </button>
        </div>

        {error && <p className="error">{error}</p>}

        {loading && <p className="muted">Loading applications…</p>}

        {!loading && applications.length === 0 && (
          <p className="muted">No applications yet. Add your first one above.</p>
        )}

        {!loading && applications.length > 0 && (
          <ul className="application-list">
            {applications.map((application) => (
              <li key={application.id} className="application-item">
                <div className="application-main">
                  <strong>{application.company}</strong>
                  <span>{application.role}</span>
                  <span className="mono">Applied {formatDate(application.appliedAt)}</span>
                  {application.notes && <p className="notes">{application.notes}</p>}
                </div>
                <div className="application-actions">
                  <select
                    value={application.status}
                    onChange={(e) =>
                      void handleStatusChange(application.id, e.target.value as ApplicationStatus)
                    }
                    aria-label={`Status for ${application.company}`}
                  >
                    {APPLICATION_STATUSES.map((status) => (
                      <option key={status} value={status}>
                        {STATUS_LABELS[status]}
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className="btn danger"
                    onClick={() => void handleDelete(application.id)}
                  >
                    Delete
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  )
}
