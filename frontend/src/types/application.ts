export type ApplicationStatus =
  | 'saved'
  | 'applied'
  | 'interview'
  | 'offer'
  | 'rejected'

export type ApplicationEvent = {
  id: string
  applicationId: string
  fromStatus: ApplicationStatus | null
  toStatus: ApplicationStatus
  createdAt: string
}

export type JobApplication = {
  id: string
  company: string
  role: string
  status: ApplicationStatus
  appliedAt: string | null
  notes: string | null
  createdAt: string
  updatedAt: string
  events: ApplicationEvent[]
}

export type StackStatus = {
  api: boolean
  postgres: boolean
  postgresError: string | null
  ragApi: boolean
  ragError: string | null
}

export type CreateApplicationInput = {
  company: string
  role: string
  appliedAt?: string | null
  notes?: string | null
}

export type UpdateApplicationInput = Partial<
  CreateApplicationInput & { status: ApplicationStatus }
>

export const APPLICATION_STATUSES: ApplicationStatus[] = [
  'saved',
  'applied',
  'interview',
  'offer',
  'rejected',
]

export const STATUS_LABELS: Record<ApplicationStatus, string> = {
  saved: 'Saved',
  applied: 'Applied',
  interview: 'Interview',
  offer: 'Offer',
  rejected: 'Rejected',
}

export const STATUS_TRANSITIONS: Record<ApplicationStatus, ApplicationStatus[]> = {
  saved: ['applied'],
  applied: ['interview'],
  interview: ['offer', 'rejected'],
  offer: [],
  rejected: [],
}

export function getSelectableStatuses(current: ApplicationStatus): ApplicationStatus[] {
  return [current, ...STATUS_TRANSITIONS[current]]
}

export function formatEventLabel(event: ApplicationEvent): string {
  if (event.fromStatus === null) {
    return `Created as ${STATUS_LABELS[event.toStatus]}`
  }

  return `${STATUS_LABELS[event.fromStatus]} → ${STATUS_LABELS[event.toStatus]}`
}
