import { NextRequest, NextResponse } from "next/server"
import {
  buildKeycloakCallbackUrl,
  createPkcePair,
  createRandomState,
  getKeycloakWebConfig,
  getTransientAuthCookieOptions,
  KEYCLOAK_CODE_VERIFIER_COOKIE_NAME,
  KEYCLOAK_NEXT_COOKIE_NAME,
  KEYCLOAK_STATE_COOKIE_NAME,
  toSafeNextPath,
} from "@/lib/server/keycloak"

export async function GET(req: NextRequest) {
  const config = getKeycloakWebConfig()
  if (!config) {
    return NextResponse.redirect(new URL("/login?error=Keycloak%20is%20not%20configured", req.url))
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

  if (mode === "signup") {
    authUrl.searchParams.set("kc_action", "register")
  }

  const response = NextResponse.redirect(authUrl)
  const cookieOptions = getTransientAuthCookieOptions()
  response.cookies.set(KEYCLOAK_STATE_COOKIE_NAME, state, cookieOptions)
  response.cookies.set(KEYCLOAK_CODE_VERIFIER_COOKIE_NAME, verifier, cookieOptions)
  response.cookies.set(KEYCLOAK_NEXT_COOKIE_NAME, nextPath, cookieOptions)
  return response
}
