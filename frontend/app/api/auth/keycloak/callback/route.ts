import { NextRequest, NextResponse } from "next/server"
import { ACCESS_TOKEN_COOKIE_NAME, getAuthCookieOptions } from "@/lib/server/auth"
import {
  buildKeycloakCallbackUrl,
  clearTransientAuthCookieOptions,
  getPublicRequestOrigin,
  getKeycloakWebConfig,
  KEYCLOAK_CODE_VERIFIER_COOKIE_NAME,
  KEYCLOAK_ID_TOKEN_COOKIE_NAME,
  KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME,
  KEYCLOAK_NEXT_COOKIE_NAME,
  KEYCLOAK_STATE_COOKIE_NAME,
  toSafeNextPath,
} from "@/lib/server/keycloak"

export const dynamic = "force-dynamic"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendAuthResponse {
  accessToken: string
  expiresAtUtc: string
  user: {
    id: number
    email: string
    displayName: string
  }
}

function buildLoginRedirect(req: NextRequest, message: string) {
  const loginUrl = new URL("/login", getPublicRequestOrigin(req))
  loginUrl.searchParams.set("error", message)
  return loginUrl
}

function clearKeycloakTransientCookies(response: NextResponse) {
  const clearCookieOptions = clearTransientAuthCookieOptions()
  response.cookies.set(KEYCLOAK_STATE_COOKIE_NAME, "", clearCookieOptions)
  response.cookies.set(KEYCLOAK_CODE_VERIFIER_COOKIE_NAME, "", clearCookieOptions)
  response.cookies.set(KEYCLOAK_NEXT_COOKIE_NAME, "", clearCookieOptions)
  response.cookies.set(KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME, "", clearCookieOptions)
}

export async function GET(req: NextRequest) {
  const config = getKeycloakWebConfig()
  if (!config) {
    return NextResponse.redirect(buildLoginRedirect(req, "Keycloak is not configured"))
  }

  const error = req.nextUrl.searchParams.get("error")
  if (error) {
    const errorDescription = req.nextUrl.searchParams.get("error_description")
    console.warn("Keycloak callback returned error", {
      error,
      errorDescription,
      path: req.nextUrl.pathname,
    })
    const response = NextResponse.redirect(
      buildLoginRedirect(req, errorDescription ?? error)
    )
    clearKeycloakTransientCookies(response)
    return response
  }

  const code = req.nextUrl.searchParams.get("code")
  const state = req.nextUrl.searchParams.get("state")
  const expectedState = req.cookies.get(KEYCLOAK_STATE_COOKIE_NAME)?.value
  const codeVerifier = req.cookies.get(KEYCLOAK_CODE_VERIFIER_COOKIE_NAME)?.value
  const nextPath = toSafeNextPath(req.cookies.get(KEYCLOAK_NEXT_COOKIE_NAME)?.value ?? null)

  if (!code || !state || !expectedState || state !== expectedState || !codeVerifier) {
    console.warn("Invalid Keycloak callback state", {
      hasCode: Boolean(code),
      hasState: Boolean(state),
      hasExpectedState: Boolean(expectedState),
      stateMatches: Boolean(state && expectedState && state === expectedState),
      hasCodeVerifier: Boolean(codeVerifier),
      userAgent: req.headers.get("user-agent"),
    })
    const response = NextResponse.redirect(buildLoginRedirect(req, "Invalid sign-in callback"))
    clearKeycloakTransientCookies(response)
    return response
  }

  try {
    const callbackUrl = buildKeycloakCallbackUrl(req)
    const tokenUrl = `${config.authority}/protocol/openid-connect/token`

    const tokenRequestBody = new URLSearchParams({
      grant_type: "authorization_code",
      code,
      client_id: config.clientId,
      redirect_uri: callbackUrl,
      code_verifier: codeVerifier,
    })

    if (config.clientSecret) {
      tokenRequestBody.set("client_secret", config.clientSecret)
    }

    const tokenResponse = await fetch(tokenUrl, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: tokenRequestBody.toString(),
      cache: "no-store",
    })

    const tokenData = await tokenResponse.json().catch(() => ({}))
    if (!tokenResponse.ok || typeof tokenData?.id_token !== "string") {
      console.warn("Keycloak token exchange failed", {
        status: tokenResponse.status,
        hasIdToken: typeof tokenData?.id_token === "string",
      })
      const response = NextResponse.redirect(buildLoginRedirect(req, "Keycloak sign-in failed"))
      clearKeycloakTransientCookies(response)
      return response
    }

    const backendResponse = await fetch(`${BACKEND_API_URL}/api/auth/keycloak/exchange`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ idToken: tokenData.id_token }),
      cache: "no-store",
    })

    const backendData = await backendResponse.json().catch(() => ({}))
    if (!backendResponse.ok) {
      const errorMessage =
        typeof backendData?.error === "string" ? backendData.error : "Unable to complete sign-in"
      console.warn("Backend Keycloak exchange failed", {
        status: backendResponse.status,
        errorMessage,
      })
      const response = NextResponse.redirect(buildLoginRedirect(req, errorMessage))
      clearKeycloakTransientCookies(response)
      return response
    }

    const authData = backendData as BackendAuthResponse
    if (!authData.accessToken) {
      const response = NextResponse.redirect(buildLoginRedirect(req, "Invalid auth response"))
      clearKeycloakTransientCookies(response)
      return response
    }

    const redirectUrl = new URL(nextPath, getPublicRequestOrigin(req))
    const response = NextResponse.redirect(redirectUrl)
    response.cookies.set(ACCESS_TOKEN_COOKIE_NAME, authData.accessToken, getAuthCookieOptions())
    response.cookies.set(KEYCLOAK_ID_TOKEN_COOKIE_NAME, tokenData.id_token, getAuthCookieOptions())
    clearKeycloakTransientCookies(response)
    return response
  } catch (error) {
    console.error("Keycloak callback error:", error)
    const response = NextResponse.redirect(buildLoginRedirect(req, "Unable to complete sign-in"))
    clearKeycloakTransientCookies(response)
    return response
  }
}
