import { NextRequest, NextResponse } from "next/server"
import { ACCESS_TOKEN_COOKIE_NAME } from "@/lib/server/auth"

const PUBLIC_ROUTES = new Set(["/login", "/signup"])

export function proxy(req: NextRequest) {
  const { pathname } = req.nextUrl

  if (
    pathname.startsWith("/_next") ||
    pathname.startsWith("/favicon.ico") ||
    pathname.startsWith("/placeholder")
  ) {
    return NextResponse.next()
  }

  if (pathname.startsWith("/api/auth")) {
    return NextResponse.next()
  }

  const hasToken = Boolean(req.cookies.get(ACCESS_TOKEN_COOKIE_NAME)?.value)

  if (pathname.startsWith("/api")) {
    if (!hasToken) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }
    return NextResponse.next()
  }

  if (PUBLIC_ROUTES.has(pathname)) {
    if (hasToken) {
      return NextResponse.redirect(new URL("/", req.url))
    }
    return NextResponse.next()
  }

  if (!hasToken) {
    const loginUrl = new URL("/login", req.url)
    loginUrl.searchParams.set("next", pathname + req.nextUrl.search)
    return NextResponse.redirect(loginUrl)
  }

  return NextResponse.next()
}

export const config = {
  matcher: ["/((?!_next/static|_next/image).*)"],
}
