import { NextRequest } from "next/server"

export const ACCESS_TOKEN_COOKIE_NAME = "aoc_access_token"
export const ACCESS_TOKEN_TTL_SECONDS = 60 * 60 * 8

export function getAccessToken(req: NextRequest): string | null {
  return req.cookies.get(ACCESS_TOKEN_COOKIE_NAME)?.value ?? null
}

export function buildBackendHeaders(
  req: NextRequest,
  options?: { includeContentType?: boolean; extraHeaders?: HeadersInit }
): Headers {
  const headers = new Headers(options?.extraHeaders)

  if (options?.includeContentType ?? true) {
    if (!headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json")
    }
  }

  const token = getAccessToken(req)
  if (token) {
    headers.set("Authorization", `Bearer ${token}`)
  }

  return headers
}

export function getAuthCookieOptions() {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict" as const,
    path: "/",
    maxAge: ACCESS_TOKEN_TTL_SECONDS,
  }
}
