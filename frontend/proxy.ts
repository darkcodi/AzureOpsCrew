import { NextRequest, NextResponse } from "next/server"
import { ACCESS_TOKEN_COOKIE_NAME } from "@/lib/server/auth"

export function proxy(req: NextRequest) {
  const { pathname } = req.nextUrl

  if (
    pathname.startsWith("/_next") ||
    pathname.startsWith("/favicon.ico") ||
    pathname.startsWith("/placeholder")
  ) {
    return NextResponse.next()
  }

  // Let all auth routes through (includes auto-login)
  if (pathname === "/api/auth" || pathname.startsWith("/api/auth/")) {
    return NextResponse.next()
  }

  const hasToken = Boolean(req.cookies.get(ACCESS_TOKEN_COOKIE_NAME)?.value)

  if (pathname.startsWith("/api")) {
    if (!hasToken) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }
    return NextResponse.next()
  }

  // Allow root page through even without token — auto-login handles auth
  return NextResponse.next()
}
