"use client"

import Link from "next/link"

export default function SignupPage() {
  const keycloakSignupHref = "/api/auth/keycloak/start?mode=signup"

  return (
    <main className="flex h-dvh w-full items-center justify-center bg-slate-950 px-4">
      <section className="w-full max-w-md rounded-xl border border-slate-800 bg-slate-900 p-6 shadow-2xl">
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-2xl font-semibold text-white">Sign up</h1>
          <Link
            href="/login"
            className="rounded-md border border-slate-700 px-3 py-1.5 text-sm text-slate-200 transition hover:bg-slate-800"
          >
            Sign in
          </Link>
        </div>

        <div className="space-y-4">
          <Link
            href={keycloakSignupHref}
            className="block w-full rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-center font-medium text-white transition hover:bg-slate-700"
          >
            Continue with Sign Up
          </Link>

          <div className="rounded-md border border-slate-800 bg-slate-950/60 px-3 py-2 text-sm text-slate-300">
            Registration is handled only through Keycloak.
          </div>
        </div>
      </section>
    </main>
  )
}
