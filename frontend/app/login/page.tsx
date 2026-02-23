import Link from "next/link"
import { unstable_noStore as noStore } from "next/cache"
import { AuthStartRedirect } from "@/components/auth-start-redirect"
import { getKeycloakAuthFeatureConfig } from "@/lib/server/keycloak"

export const dynamic = "force-dynamic"

function toSafeNextPath(next: string | string[] | undefined): string {
  const value = Array.isArray(next) ? next[0] : next
  if (!value) return "/"
  if (!value.startsWith("/") || value.startsWith("//")) return "/"
  return value
}

type LoginPageProps = {
  searchParams: Promise<{
    next?: string | string[]
    error?: string | string[]
    loggedOut?: string | string[]
  }>
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  noStore()
  const params = await searchParams
  const nextPath = toSafeNextPath(params.next)
  const error = Array.isArray(params.error) ? params.error[0] : params.error
  const loggedOut = Array.isArray(params.loggedOut) ? params.loggedOut[0] : params.loggedOut
  const features = getKeycloakAuthFeatureConfig()
  const loginStartHref = `/api/auth/keycloak/start?mode=login&next=${encodeURIComponent(nextPath)}`

  if (!error && loggedOut !== "1") {
    return (
      <AuthStartRedirect
        href={loginStartHref}
        title="Redirecting to sign in"
        description="Opening secure sign-in..."
      />
    )
  }

  return (
    <main className="flex h-dvh w-full items-center justify-center bg-slate-950 px-4">
      <section className="w-full max-w-md rounded-xl border border-slate-800 bg-slate-900 p-6 shadow-2xl">
        <h1 className="mb-3 text-2xl font-semibold text-white">
          {error ? "Sign in failed" : "Signed out"}
        </h1>
        <p className={`mb-4 text-sm ${error ? "text-red-300" : "text-slate-300"}`}>
          {error ?? "You have been signed out."}
        </p>
        <div className="flex gap-3">
          <a
            href={loginStartHref}
            className="rounded-md bg-sky-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-sky-500"
          >
            {error ? "Try again" : "Sign in"}
          </a>
          {features.localSignupEnabled ? (
            <Link
              href="/signup"
              className="rounded-md border border-slate-700 px-3 py-2 text-sm text-slate-200 transition hover:bg-slate-800"
            >
              Sign up
            </Link>
          ) : null}
        </div>
      </section>
    </main>
  )
}
