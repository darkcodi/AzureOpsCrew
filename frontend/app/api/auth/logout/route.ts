import { NextRequest, NextResponse } from "next/server"
import { ACCESS_TOKEN_COOKIE_NAME, getAuthCookieOptions } from "@/lib/server/auth"
import {
  clearTransientAuthCookieOptions,
  getKeycloakWebConfig,
  getPublicRequestOrigin,
  KEYCLOAK_CODE_VERIFIER_COOKIE_NAME,
  KEYCLOAK_ID_TOKEN_COOKIE_NAME,
  KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME,
  KEYCLOAK_NEXT_COOKIE_NAME,
  KEYCLOAK_STATE_COOKIE_NAME,
} from "@/lib/server/keycloak"

function clearAuthCookies(response: NextResponse) {
  const authCookieOptions = { ...getAuthCookieOptions(), maxAge: 0 }
  const transientCookieOptions = clearTransientAuthCookieOptions()

  response.cookies.set(ACCESS_TOKEN_COOKIE_NAME, "", authCookieOptions)
  response.cookies.set(KEYCLOAK_ID_TOKEN_COOKIE_NAME, "", authCookieOptions)
  response.cookies.set(KEYCLOAK_STATE_COOKIE_NAME, "", transientCookieOptions)
  response.cookies.set(KEYCLOAK_CODE_VERIFIER_COOKIE_NAME, "", transientCookieOptions)
  response.cookies.set(KEYCLOAK_NEXT_COOKIE_NAME, "", transientCookieOptions)
  response.cookies.set(KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME, "", transientCookieOptions)
}

function buildKeycloakLogoutRedirect(req: NextRequest): URL | null {
  const config = getKeycloakWebConfig()
  if (!config) return null

  const logoutUrl = new URL(`${config.authority}/protocol/openid-connect/logout`)
  const postLogoutRedirectUrl = new URL("/login", getPublicRequestOrigin(req))
  logoutUrl.searchParams.set("post_logout_redirect_uri", postLogoutRedirectUrl.toString())
  logoutUrl.searchParams.set("client_id", config.clientId)

  const idTokenHint = req.cookies.get(KEYCLOAK_ID_TOKEN_COOKIE_NAME)?.value
  if (idTokenHint) {
    logoutUrl.searchParams.set("id_token_hint", idTokenHint)
  }

  return logoutUrl
}

export async function GET(req: NextRequest) {
  const fallbackLoginUrl = new URL("/login", getPublicRequestOrigin(req))
  const keycloakLogoutUrl = buildKeycloakLogoutRedirect(req)
  const response = NextResponse.redirect(keycloakLogoutUrl ?? fallbackLoginUrl)
  clearAuthCookies(response)
  return response
}

export async function POST() {
  const response = NextResponse.json({ ok: true })
  clearAuthCookies(response)
  return response
}
