import { NextRequest, NextResponse } from "next/server"
import {
  buildKeycloakCallbackUrl,
  createPkcePair,
  createRandomState,
  getKeycloakAuthFeatureConfig,
  getPublicRequestOrigin,
  getKeycloakWebConfig,
  getTransientAuthCookieOptions,
  KEYCLOAK_CODE_VERIFIER_COOKIE_NAME,
  KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME,
  KEYCLOAK_NEXT_COOKIE_NAME,
  KEYCLOAK_STATE_COOKIE_NAME,
  parseLoginAttemptCount,
  toSafeNextPath,
} from "@/lib/server/keycloak"

export const dynamic = "force-dynamic"

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

function rewriteCookieDomainForAoc(cookie: string, publicOrigin: string): string {
  if (/;\s*domain=/i.test(cookie)) return cookie
  const hostname = new URL(publicOrigin).hostname
  if (!hostname.endsWith(".aoc-app.com")) return cookie
  return `${cookie}; Domain=.aoc-app.com`
}

export async function GET(req: NextRequest) {
  const publicOrigin = getPublicRequestOrigin(req)
  const config = getKeycloakWebConfig()
  const features = getKeycloakAuthFeatureConfig()
  if (!config) {
    return NextResponse.redirect(new URL("/login?error=Keycloak%20is%20not%20configured", publicOrigin))
  }

  const mode = req.nextUrl.searchParams.get("mode")
  const nextPath = toSafeNextPath(req.nextUrl.searchParams.get("next"))
  const currentAttemptCount = parseLoginAttemptCount(
    req.cookies.get(KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME)?.value
  )

  // Stop browser redirect loops (Safari/private mode or blocked cookies can trigger this)
  // and surface an actionable error instead of infinite auth hops.
  if (mode !== "signup" && currentAttemptCount >= 5) {
    const loginUrl = new URL("/login", publicOrigin)
    loginUrl.searchParams.set(
      "error",
      "Too many sign-in redirects. Clear site cookies and try again."
    )

    const response = NextResponse.redirect(loginUrl)
    response.cookies.set(KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME, "0", {
      ...getTransientAuthCookieOptions(),
      maxAge: 0,
    })
    return response
  }

  if (mode === "signup" && !features.localSignupEnabled) {
    return NextResponse.redirect(new URL(`/login?error=${encodeURIComponent("Sign up is disabled")}`, publicOrigin))
  }

  if (mode !== "signup" && !features.localLoginEnabled && !features.entraSsoEnabled) {
    return NextResponse.redirect(
      new URL(`/login?error=${encodeURIComponent("All sign-in methods are disabled")}`, publicOrigin)
    )
  }

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
  if (mode !== "signup" && !features.localLoginEnabled && features.entraSsoEnabled) {
    // In Entra-only mode, force a fresh brokered auth so Keycloak doesn't silently
    // reuse an existing SSO session after group membership was changed in Entra.
    authUrl.searchParams.set("kc_idp_hint", features.entraIdpHint)
    authUrl.searchParams.set("prompt", "login")
    authUrl.searchParams.set("max_age", "0")
  }

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
            .map((cookie) => rewriteCookieDomainForAoc(cookie, publicOrigin))
        }
      }
    } catch {
      // Fall back to the normal auth URL if the preflight bootstrap fails.
    }
  }

  const response = NextResponse.redirect(redirectUrl)
  response.cookies.set(KEYCLOAK_STATE_COOKIE_NAME, state, getTransientAuthCookieOptions())
  response.cookies.set(KEYCLOAK_CODE_VERIFIER_COOKIE_NAME, verifier, getTransientAuthCookieOptions())
  response.cookies.set(KEYCLOAK_NEXT_COOKIE_NAME, nextPath, getTransientAuthCookieOptions())
  response.cookies.set(
    KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME,
    String(mode === "signup" ? 0 : currentAttemptCount + 1),
    getTransientAuthCookieOptions()
  )

  for (const setCookie of upstreamKeycloakCookies) {
    response.headers.append("set-cookie", setCookie)
  }

  return response
}
