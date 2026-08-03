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

type SortBy = 'applied' | 'company' | 'role' | 'status' | 'created' | 'updated'
type SortDir = 'asc' | 'desc'

type ListFilters = {
  status: '' | ApplicationStatus
  search: string
  sortBy: SortBy
  sortDir: SortDir
}

async function fetchApplications(filters: ListFilters): Promise<JobApplication[]> {
  const params = new URLSearchParams()
  if (filters.status) params.set('status', filters.status)
  if (filters.search.trim()) params.set('search', filters.search.trim())
  params.set('sortBy', filters.sortBy)
  params.set('sortDir', filters.sortDir)
  const response = await fetch(`/api/applications?${params.toString()}`)
  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as { error?: string } | null
    throw new Error(body?.error ?? `HTTP ${response.status}`)
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
  const [filters, setFilters] = useState<ListFilters>({
    status: '',
    search: '',
    sortBy: 'applied',
    sortDir: 'desc',
  })
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async (next: ListFilters = filters) => {
    setLoading(true)
    setError(null)
    try {
      setApplications(await fetchApplications(next))
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to load applications')
    } finally {
      setLoading(false)
    }
  }, [filters])

  useEffect(() => {
    let cancelled = false

    fetchApplications(filters)
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
  }, [filters])

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

      const created = (await response.json()) as JobApplication
      setForm(emptyForm)
      // Optimistic insert when filters would include the new row; otherwise refresh.
      if (
        (!filters.status || filters.status === created.status) &&
        (!filters.search.trim() ||
          created.company.toLowerCase().includes(filters.search.trim().toLowerCase()) ||
          created.role.toLowerCase().includes(filters.search.trim().toLowerCase()))
      ) {
        setApplications((prev) => [created, ...prev.filter((a) => a.id !== created.id)])
      } else {
        await refresh()
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to create application')
    } finally {
      setSubmitting(false)
    }
  }

  const handleStatusChange = async (id: string, status: ApplicationStatus) => {
    setError(null)
    const previous = applications
    setApplications((prev) =>
      prev
        .map((app) => (app.id === id ? { ...app, status } : app))
        .filter((app) => !filters.status || app.status === filters.status),
    )
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
      const updated = (await response.json()) as JobApplication
      setApplications((prev) => {
        const without = prev.filter((a) => a.id !== id)
        if (filters.status && updated.status !== filters.status) {
          return without
        }
        return [updated, ...without]
      })
    } catch (e: unknown) {
      setApplications(previous)
      setError(e instanceof Error ? e.message : 'Failed to update status')
    }
  }

  const handleDelete = async (id: string) => {
    setError(null)
    const previous = applications
    setApplications((prev) => prev.filter((a) => a.id !== id))
    try {
      const response = await fetch(`/api/applications/${id}`, { method: 'DELETE' })
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }
    } catch (e: unknown) {
      setApplications(previous)
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

        <div className="filters">
          <label>
            Status
            <select
              value={filters.status}
              onChange={(e) =>
                setFilters((prev) => ({
                  ...prev,
                  status: e.target.value as ListFilters['status'],
                }))
              }
            >
              <option value="">All</option>
              {APPLICATION_STATUSES.map((status) => (
                <option key={status} value={status}>
                  {STATUS_LABELS[status]}
                </option>
              ))}
            </select>
          </label>
          <label>
            Search
            <input
              value={filters.search}
              onChange={(e) => setFilters((prev) => ({ ...prev, search: e.target.value }))}
              placeholder="Company or role"
            />
          </label>
          <label>
            Sort by
            <select
              value={filters.sortBy}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, sortBy: e.target.value as SortBy }))
              }
            >
              <option value="applied">Applied date</option>
              <option value="company">Company</option>
              <option value="role">Role</option>
              <option value="status">Status</option>
              <option value="created">Created</option>
              <option value="updated">Updated</option>
            </select>
          </label>
          <label>
            Direction
            <select
              value={filters.sortDir}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, sortDir: e.target.value as SortDir }))
              }
            >
              <option value="desc">Descending</option>
              <option value="asc">Ascending</option>
            </select>
          </label>
        </div>

        {error && <p className="error">{error}</p>}

        {loading && <p className="muted">Loading applications…</p>}

        {!loading && applications.length === 0 && (
          <p className="muted">
            {filters.status || filters.search.trim()
              ? 'No applications match these filters.'
              : 'No applications yet. Add your first one above.'}
          </p>
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
