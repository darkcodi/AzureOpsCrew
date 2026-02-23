"use client"

import { useEffect } from "react"

interface AuthStartRedirectProps {
  href: string
  title: string
  description: string
}

export function AuthStartRedirect({ href, title, description }: AuthStartRedirectProps) {
  useEffect(() => {
    window.location.replace(href)
  }, [href])

  return (
    <main className="flex h-dvh w-full items-center justify-center bg-slate-950 px-4">
      <section className="w-full max-w-md rounded-xl border border-slate-800 bg-slate-900 p-6 shadow-2xl">
        <h1 className="mb-3 text-2xl font-semibold text-white">{title}</h1>
        <p className="mb-4 text-sm text-slate-300">{description}</p>
        <a
          href={href}
          className="inline-flex rounded-md bg-sky-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-sky-500"
        >
          Continue
        </a>
      </section>
    </main>
  )
}
