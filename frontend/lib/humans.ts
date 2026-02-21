export type HumanStatus = "Online" | "Offline"

export interface HumanMember {
  id: string
  userId: number
  name: string
  email: string
  status: HumanStatus
  isCurrentUser: boolean
}

const HUMAN_ID_PREFIX = "human:"

export function toHumanCardId(userId: number): string {
  return `${HUMAN_ID_PREFIX}${userId}`
}

export function isHumanCardId(id: string): boolean {
  return id.startsWith(HUMAN_ID_PREFIX)
}
