import { NextRequest, NextResponse } from "next/server"
import {
  buildKeycloakCallbackUrl,
  createPkcePair,
  createRandomState,
  getPublicRequestOrigin,
  getKeycloakWebConfig,
  getTransientAuthCookieOptions,
  KEYCLOAK_CODE_VERIFIER_COOKIE_NAME,
  KEYCLOAK_NEXT_COOKIE_NAME,
  KEYCLOAK_STATE_COOKIE_NAME,
  toSafeNextPath,
} from "@/lib/server/keycloak"

function htmlDecode(value: string): string {
  return value
    .replace(/&amp;/g, "&")
    .replace(/&quot;/g, "\"")
    .replace(/&#39;/g, "'")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
}

function extractRegistrationUrl(loginPageHtml: string, authority: string): URL | null {
  const match = loginPageHtml.match(/href="([^"]*login-actions\/registration[^"]*)"/i)
  if (!match?.[1]) return null

  try {
    return new URL(htmlDecode(match[1]), authority)
  } catch {
    return null
  }
}

function getUpstreamSetCookies(headers: Headers): string[] {
  const typedHeaders = headers as Headers & { getSetCookie?: () => string[] }
  if (typeof typedHeaders.getSetCookie === "function") {
    return typedHeaders.getSetCookie()
  }

  const single = headers.get("set-cookie")
  return single ? [single] : []
}

function rewriteCookieDomainForAoc(cookie: string): string {
  if (/;\s*domain=/i.test(cookie)) return cookie
  return `${cookie}; Domain=.aoc-app.com`
}

export async function GET(req: NextRequest) {
  const publicOrigin = getPublicRequestOrigin(req)
  const config = getKeycloakWebConfig()
  if (!config) {
    return NextResponse.redirect(new URL("/login?error=Keycloak%20is%20not%20configured", publicOrigin))
  }

  const mode = req.nextUrl.searchParams.get("mode")
  const nextPath = toSafeNextPath(req.nextUrl.searchParams.get("next"))
  const callbackUrl = buildKeycloakCallbackUrl(req)

  const state = createRandomState()
  const { verifier, challenge } = createPkcePair()

  const authUrl = new URL(`${config.authority}/protocol/openid-connect/auth`)
  authUrl.searchParams.set("client_id", config.clientId)
  authUrl.searchParams.set("response_type", "code")
  authUrl.searchParams.set("scope", "openid profile email")
  authUrl.searchParams.set("redirect_uri", callbackUrl)
  authUrl.searchParams.set("state", state)
  authUrl.searchParams.set("code_challenge", challenge)
  authUrl.searchParams.set("code_challenge_method", "S256")

  let redirectUrl = authUrl
  let upstreamKeycloakCookies: string[] = []

  if (mode === "signup") {
    try {
      const preflight = await fetch(authUrl, {
        method: "GET",
        redirect: "manual",
        cache: "no-store",
      })

      if (preflight.ok) {
        const html = await preflight.text()
        const registrationUrl = extractRegistrationUrl(html, config.authority)
        if (registrationUrl) {
          redirectUrl = registrationUrl
          upstreamKeycloakCookies = getUpstreamSetCookies(preflight.headers)
            .map(rewriteCookieDomainForAoc)
        }
      }
    } catch {
      // Fall back to the normal auth URL if the preflight bootstrap fails.
    }
  }

  const response = NextResponse.redirect(redirectUrl)
  const cookieOptions = getTransientAuthCookieOptions()
  response.cookies.set(KEYCLOAK_STATE_COOKIE_NAME, state, cookieOptions)
  response.cookies.set(KEYCLOAK_CODE_VERIFIER_COOKIE_NAME, verifier, cookieOptions)
  response.cookies.set(KEYCLOAK_NEXT_COOKIE_NAME, nextPath, cookieOptions)

  for (const setCookie of upstreamKeycloakCookies) {
    response.headers.append("set-cookie", setCookie)
  }

  return response
}
