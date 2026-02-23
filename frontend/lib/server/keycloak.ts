import { createHash, randomBytes } from "node:crypto"
import { NextRequest } from "next/server"

export const KEYCLOAK_STATE_COOKIE_NAME = "aoc_kc_state"
export const KEYCLOAK_CODE_VERIFIER_COOKIE_NAME = "aoc_kc_code_verifier"
export const KEYCLOAK_NEXT_COOKIE_NAME = "aoc_kc_next"
export const KEYCLOAK_ID_TOKEN_COOKIE_NAME = "aoc_kc_id_token"
export const KEYCLOAK_LOGIN_ATTEMPT_COOKIE_NAME = "aoc_kc_login_attempts"

export interface KeycloakWebConfig {
  authority: string
  clientId: string
  clientSecret: string | null
}

export interface KeycloakAuthFeatureConfig {
  localLoginEnabled: boolean
  localSignupEnabled: boolean
  entraSsoEnabled: boolean
  entraIdpHint: string
}

function firstHeaderValue(value: string | null): string | null {
  if (!value) return null
  const first = value.split(",")[0]?.trim()
  return first && first.length > 0 ? first : null
}

export function getKeycloakWebConfig(): KeycloakWebConfig | null {
  const authority = process.env.KEYCLOAK_AUTHORITY?.trim().replace(/\/+$/, "") ?? ""
  const clientId = process.env.KEYCLOAK_CLIENT_ID?.trim() ?? ""
  const clientSecret = process.env.KEYCLOAK_CLIENT_SECRET?.trim() ?? null

  if (!authority || !clientId) {
    return null
  }

  return {
    authority,
    clientId,
    clientSecret: clientSecret && clientSecret.length > 0 ? clientSecret : null,
  }
}

function parseBooleanEnv(name: string, fallback: boolean): boolean {
  const raw = process.env[name]
  if (!raw) return fallback

  switch (raw.trim().toLowerCase()) {
    case "1":
    case "true":
    case "yes":
    case "on":
      return true
    case "0":
    case "false":
    case "no":
    case "off":
      return false
    default:
      return fallback
  }
}

export function getKeycloakAuthFeatureConfig(): KeycloakAuthFeatureConfig {
  return {
    localLoginEnabled: parseBooleanEnv("KEYCLOAK_LOCAL_LOGIN_ENABLED", true),
    localSignupEnabled: parseBooleanEnv("KEYCLOAK_LOCAL_SIGNUP_ENABLED", true),
    entraSsoEnabled: parseBooleanEnv("KEYCLOAK_ENTRA_SSO_ENABLED", false),
    entraIdpHint: process.env.KEYCLOAK_ENTRA_IDP_HINT?.trim() || "entra",
  }
}

export function buildKeycloakCallbackUrl(req: NextRequest): string {
  const configured = process.env.KEYCLOAK_CALLBACK_URL?.trim()
  if (configured) return configured

  const url = new URL("/api/auth/keycloak/callback", getPublicRequestOrigin(req))
  return url.toString()
}

export function getPublicRequestOrigin(req: NextRequest): string {
  const configured = process.env.PUBLIC_APP_URL?.trim().replace(/\/+$/, "")
  if (configured) return configured

  const configuredCallbackUrl = process.env.KEYCLOAK_CALLBACK_URL?.trim()
  if (configuredCallbackUrl) {
    try {
      return new URL(configuredCallbackUrl).origin
    } catch {
      // Ignore invalid override and continue with header-based detection.
    }
  }

  const forwardedProto = firstHeaderValue(req.headers.get("x-forwarded-proto"))
  const forwardedHost =
    firstHeaderValue(req.headers.get("x-forwarded-host")) ??
    firstHeaderValue(req.headers.get("host"))

  if (forwardedProto && forwardedHost) {
    return `${forwardedProto}://${forwardedHost}`
  }

  return req.nextUrl.origin
}

export function toSafeNextPath(next: string | null): string {
  if (!next) return "/"
  if (!next.startsWith("/") || next.startsWith("//")) return "/"
  return next
}

export function base64UrlEncode(value: Buffer): string {
  return value
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/g, "")
}

export function createPkcePair() {
  const verifier = base64UrlEncode(randomBytes(32))
  const challenge = base64UrlEncode(createHash("sha256").update(verifier).digest())

  return { verifier, challenge }
}

export function createRandomState() {
  return base64UrlEncode(randomBytes(24))
}

export function getTransientAuthCookieOptions() {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge: 60 * 10,
  }
}

export function clearTransientAuthCookieOptions() {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge: 0,
  }
}

export function parseLoginAttemptCount(value: string | null | undefined): number {
  if (!value) return 0
  const parsed = Number.parseInt(value, 10)
  if (!Number.isFinite(parsed) || parsed < 0) return 0
  return parsed
}
