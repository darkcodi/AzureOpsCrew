export type HumanStatus = "Online" | "Offline"

export interface HumanMember {
  id: string
  userId: number
  name: string
  status: HumanStatus
  isCurrentUser: boolean
}

const HUMAN_ID_PREFIX = "human:"
const HUMANS_CACHE_KEY = "aoc_humans_cache_v1"

export function toHumanCardId(userId: number): string {
  return `${HUMAN_ID_PREFIX}${userId}`
}

export function isHumanCardId(id: string): boolean {
  return id.startsWith(HUMAN_ID_PREFIX)
}

export function getCachedHumans(): HumanMember[] {
  if (typeof window === "undefined") return []

  try {
    const raw = window.sessionStorage.getItem(HUMANS_CACHE_KEY)
    if (!raw) return []

    const parsed = JSON.parse(raw)
    if (!Array.isArray(parsed)) return []

    return parsed.filter(
      (item): item is HumanMember =>
        item &&
        typeof item.id === "string" &&
        typeof item.userId === "number" &&
        typeof item.name === "string" &&
        (item.status === "Online" || item.status === "Offline") &&
        typeof item.isCurrentUser === "boolean"
    )
  } catch {
    return []
  }
}

export function setCachedHumans(humans: HumanMember[]): void {
  if (typeof window === "undefined") return

  try {
    window.sessionStorage.setItem(HUMANS_CACHE_KEY, JSON.stringify(humans))
  } catch {
    // Ignore storage failures.
  }
}

export function clearCachedHumans(): void {
  if (typeof window === "undefined") return

  try {
    window.sessionStorage.removeItem(HUMANS_CACHE_KEY)
  } catch {
    // Ignore storage failures.
  }
}
