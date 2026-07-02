export type ApplicationStatus =
  | 'saved'
  | 'applied'
  | 'interview'
  | 'offer'
  | 'rejected'

export type JobApplication = {
  id: string
  company: string
  role: string
  status: ApplicationStatus
  appliedAt: string | null
  notes: string | null
  createdAt: string
  updatedAt: string
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
  status?: ApplicationStatus
  appliedAt?: string | null
  notes?: string | null
}

export type UpdateApplicationInput = Partial<CreateApplicationInput>

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
