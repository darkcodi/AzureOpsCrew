import { cookies } from "next/headers"
import HomePageClient from "@/components/home-page-client"
import { toHumanCardId, type HumanMember } from "@/lib/humans"
import { ACCESS_TOKEN_COOKIE_NAME } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendUserPresence {
  id: string
  username: string
  isOnline: boolean
  isCurrentUser: boolean
}

export const dynamic = "force-dynamic"

async function loadInitialHumans(): Promise<HumanMember[]> {
  const cookieStore = await cookies()
  const accessToken = cookieStore.get(ACCESS_TOKEN_COOKIE_NAME)?.value

  if (!accessToken) {
    return []
  }

  try {
    const response = await fetch(`${BACKEND_API_URL}/api/users`, {
      method: "GET",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
      cache: "no-store",
    })

    if (!response.ok) {
      return []
    }

    const data = (await response.json()) as BackendUserPresence[]
    return data.map((user): HumanMember => ({
      id: toHumanCardId(user.id),
      userId: user.id,
      name: user.username || "User",
      status: user.isOnline ? "Online" : "Offline",
      isCurrentUser: user.isCurrentUser,
    }))
  } catch {
    return []
  }
}

export default async function HomePage() {
  const initialHumans = await loadInitialHumans()
  return <HomePageClient initialHumans={initialHumans} />
}
