"use client"

import { Suspense } from "react"
import Link from "next/link"
import { useSearchParams } from "next/navigation"

function toSafeNextPath(next: string | null): string {
  if (!next) {
    return "/"
  }

  if (!next.startsWith("/") || next.startsWith("//")) {
    return "/"
  }

  return next
}

function LoginPageContent() {
  const searchParams = useSearchParams()
  const nextPath = toSafeNextPath(searchParams.get("next"))
  const keycloakError = searchParams.get("error")
  const keycloakLoginHref = `/api/auth/keycloak/start?mode=login&next=${encodeURIComponent(nextPath)}`

  return (
    <main className="flex h-dvh w-full items-center justify-center bg-slate-950 px-4">
      <section className="w-full max-w-md rounded-xl border border-slate-800 bg-slate-900 p-6 shadow-2xl">
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-2xl font-semibold text-white">Login</h1>
          <Link
            href="/signup"
            className="rounded-md border border-slate-700 px-3 py-1.5 text-sm text-slate-200 transition hover:bg-slate-800"
          >
            Sign up
          </Link>
        </div>

        <div className="mb-4 space-y-3">
          <Link
            href={keycloakLoginHref}
            className="block w-full rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-center font-medium text-white transition hover:bg-slate-700"
          >
            Continue with Sign In
          </Link>
          <p className="text-center text-xs text-slate-400">
            Sign in is handled securely by Keycloak (OIDC + PKCE).
          </p>
        </div>

        {keycloakError && <p className="text-sm text-red-400">{keycloakError}</p>}
      </section>
    </main>
  )
}

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginPageContent />
    </Suspense>
  )
}
