import { createHash, randomBytes } from "node:crypto"
import { NextRequest } from "next/server"

export const KEYCLOAK_STATE_COOKIE_NAME = "aoc_kc_state"
export const KEYCLOAK_CODE_VERIFIER_COOKIE_NAME = "aoc_kc_code_verifier"
export const KEYCLOAK_NEXT_COOKIE_NAME = "aoc_kc_next"

export interface KeycloakWebConfig {
  authority: string
  clientId: string
  clientSecret: string | null
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

export function buildKeycloakCallbackUrl(req: NextRequest): string {
  const configured = process.env.KEYCLOAK_CALLBACK_URL?.trim()
  if (configured) return configured

  const url = new URL("/api/auth/keycloak/callback", req.url)
  return url.toString()
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
