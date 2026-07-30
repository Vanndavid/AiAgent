import {
  formatEventLabel,
  getSelectableStatuses,
  STATUS_LABELS,
  type ApplicationStatus,
  type JobApplication,
} from '../types/application'

function formatDateTime(value: string) {
  return new Date(value).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}

type ApplicationTimelineProps = {
  events: JobApplication['events']
}

export function ApplicationTimeline({ events }: ApplicationTimelineProps) {
  if (events.length === 0) {
    return null
  }

  return (
    <ol className="timeline">
      {events.map((event) => (
        <li key={event.id} className="timeline-item">
          <span className="timeline-dot" aria-hidden="true" />
          <div className="timeline-content">
            <strong>{formatEventLabel(event)}</strong>
            <span className="mono timeline-time">{formatDateTime(event.createdAt)}</span>
          </div>
        </li>
      ))}
    </ol>
  )
}

type ApplicationStatusSelectProps = {
  application: JobApplication
  onChange: (id: string, status: ApplicationStatus) => void
}

export function ApplicationStatusSelect({ application, onChange }: ApplicationStatusSelectProps) {
  const options = getSelectableStatuses(application.status)

  return (
    <select
      value={application.status}
      onChange={(e) => onChange(application.id, e.target.value as ApplicationStatus)}
      aria-label={`Status for ${application.company}`}
    >
      {options.map((status) => (
        <option key={status} value={status}>
          {STATUS_LABELS[status]}
          {status === application.status ? ' (current)' : ''}
        </option>
      ))}
    </select>
  )
}
