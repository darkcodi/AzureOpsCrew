import Link from "next/link"
import { redirect } from "next/navigation"
import { getKeycloakAuthFeatureConfig } from "@/lib/server/keycloak"

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
  }>
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const params = await searchParams
  const nextPath = toSafeNextPath(params.next)
  const error = Array.isArray(params.error) ? params.error[0] : params.error
  const features = getKeycloakAuthFeatureConfig()

  if (!error) {
    redirect(`/api/auth/keycloak/start?mode=login&next=${encodeURIComponent(nextPath)}`)
  }

  return (
    <main className="flex h-dvh w-full items-center justify-center bg-slate-950 px-4">
      <section className="w-full max-w-md rounded-xl border border-slate-800 bg-slate-900 p-6 shadow-2xl">
        <h1 className="mb-3 text-2xl font-semibold text-white">Sign in failed</h1>
        <p className="mb-4 text-sm text-red-300">{error}</p>
        <div className="flex gap-3">
          <Link
            href={`/api/auth/keycloak/start?mode=login&next=${encodeURIComponent(nextPath)}`}
            className="rounded-md bg-sky-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-sky-500"
          >
            Try again
          </Link>
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
