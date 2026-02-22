"use client"

import { FormEvent, useEffect, useMemo, useState } from "react"
import Link from "next/link"
import { useRouter } from "next/navigation"

type SignupStep = "details" | "verify"

interface RegisterChallengeResponse {
  message: string
  expiresAtUtc: string
  resendAvailableInSeconds: number
}

function parseRegisterChallengeResponse(data: unknown): RegisterChallengeResponse | null {
  if (!data || typeof data !== "object") return null

  const challenge = data as Record<string, unknown>
  if (typeof challenge.message !== "string" || challenge.message.trim().length === 0) return null
  if (typeof challenge.expiresAtUtc !== "string" || Number.isNaN(Date.parse(challenge.expiresAtUtc))) return null

  const resendSeconds = Number(challenge.resendAvailableInSeconds)
  if (!Number.isFinite(resendSeconds)) return null

  return {
    message: challenge.message,
    expiresAtUtc: challenge.expiresAtUtc,
    resendAvailableInSeconds: Math.max(0, resendSeconds),
  }
}

function formatCountdown(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${seconds.toString().padStart(2, "0")}`
}

export default function SignupPage() {
  const router = useRouter()
  const [step, setStep] = useState<SignupStep>("details")

  const [displayName, setDisplayName] = useState("")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")

  const [verificationCode, setVerificationCode] = useState("")
  const [expiresAtUtc, setExpiresAtUtc] = useState<string | null>(null)
  const [resendCooldownSeconds, setResendCooldownSeconds] = useState(0)
  const [challengeMessage, setChallengeMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isResending, setIsResending] = useState(false)
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (resendCooldownSeconds <= 0) return

    const timer = window.setInterval(() => {
      setResendCooldownSeconds((current) => Math.max(0, current - 1))
    }, 1000)

    return () => {
      window.clearInterval(timer)
    }
  }, [resendCooldownSeconds])

  useEffect(() => {
    if (!expiresAtUtc) return

    const timer = window.setInterval(() => {
      setNow(Date.now())
    }, 1000)

    return () => {
      window.clearInterval(timer)
    }
  }, [expiresAtUtc])

  const expiresInLabel = useMemo(() => {
    if (!expiresAtUtc) return null

    const diffMs = new Date(expiresAtUtc).getTime() - now
    if (diffMs <= 0) return "expired"

    const diffMinutes = Math.ceil(diffMs / 60000)
    return `${diffMinutes} minute${diffMinutes === 1 ? "" : "s"}`
  }, [expiresAtUtc, now])

  async function handleRequestCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (password !== confirmPassword) {
      setError("Passwords do not match")
      return
    }

    setIsSubmitting(true)
    try {
      const response = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email,
          password,
          displayName: displayName.trim() || undefined,
        }),
      })

      const data = await response.json().catch(() => ({}))
      if (!response.ok) {
        setError(data?.error ?? "Unable to start registration")
        return
      }

      const challenge = parseRegisterChallengeResponse(data)
      if (!challenge) {
        setError("Invalid response from server")
        return
      }

      setChallengeMessage(challenge.message)
      setExpiresAtUtc(challenge.expiresAtUtc)
      setResendCooldownSeconds(challenge.resendAvailableInSeconds)
      setVerificationCode("")
      setStep("verify")
    } catch {
      setError("Unable to start registration. Please try again.")
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleVerifyCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    const code = verificationCode.trim()
    if (!/^\d{4,8}$/.test(code)) {
      setError("Enter a valid numeric verification code")
      return
    }

    setIsSubmitting(true)
    try {
      const response = await fetch("/api/auth/register/verify", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email,
          code,
        }),
      })

      const data = await response.json().catch(() => ({}))
      if (!response.ok) {
        setError(data?.error ?? "Verification failed")
        return
      }

      router.replace("/")
      router.refresh()
    } catch {
      setError("Unable to verify code. Please try again.")
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleResendCode() {
    setError(null)
    setIsResending(true)

    try {
      const response = await fetch("/api/auth/register/resend", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      })

      const data = await response.json().catch(() => ({}))
      if (!response.ok) {
        setError(data?.error ?? "Unable to resend verification code")
        return
      }

      const challenge = parseRegisterChallengeResponse(data)
      if (!challenge) {
        setError("Invalid response from server")
        return
      }

      setChallengeMessage(challenge.message)
      setExpiresAtUtc(challenge.expiresAtUtc)
      setResendCooldownSeconds(challenge.resendAvailableInSeconds)
    } catch {
      setError("Unable to resend verification code. Please try again.")
    } finally {
      setIsResending(false)
    }
  }

  function handleChangeEmail() {
    setStep("details")
    setVerificationCode("")
    setChallengeMessage(null)
    setExpiresAtUtc(null)
    setResendCooldownSeconds(0)
    setError(null)
  }

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

        {step === "details" ? (
          <form onSubmit={handleRequestCode} className="space-y-4">
            <label className="block">
              <span className="mb-1 block text-sm text-slate-300">Display name (optional)</span>
              <input
                type="text"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                maxLength={120}
                autoComplete="name"
                className="w-full rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-white outline-none focus:border-sky-500"
              />
            </label>

            <label className="block">
              <span className="mb-1 block text-sm text-slate-300">Email</span>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoComplete="email"
                className="w-full rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-white outline-none focus:border-sky-500"
              />
            </label>

            <label className="block">
              <span className="mb-1 block text-sm text-slate-300">Password</span>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                minLength={8}
                autoComplete="new-password"
                className="w-full rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-white outline-none focus:border-sky-500"
              />
            </label>

            <label className="block">
              <span className="mb-1 block text-sm text-slate-300">Confirm password</span>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                minLength={8}
                autoComplete="new-password"
                className="w-full rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-white outline-none focus:border-sky-500"
              />
            </label>

            {error && <p className="text-sm text-red-400">{error}</p>}

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-sky-600 px-3 py-2 font-medium text-white transition hover:bg-sky-500 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSubmitting ? "Sending code..." : "Send verification code"}
            </button>
          </form>
        ) : (
          <form onSubmit={handleVerifyCode} className="space-y-4">
            <div className="rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-200">
              {challengeMessage ?? "We sent a verification code to your email."}
              <div className="mt-1 text-xs text-slate-400">
                Email: <span className="font-medium text-slate-300">{email}</span>
              </div>
              {expiresInLabel && (
                <div className="mt-1 text-xs text-slate-400">Code expires in {expiresInLabel}.</div>
              )}
            </div>

            <label className="block">
              <span className="mb-1 block text-sm text-slate-300">Verification code</span>
              <input
                type="text"
                inputMode="numeric"
                autoComplete="one-time-code"
                value={verificationCode}
                onChange={(e) => setVerificationCode(e.target.value.replace(/\D/g, "").slice(0, 8))}
                required
                minLength={4}
                maxLength={8}
                className="w-full rounded-md border border-slate-700 bg-slate-800 px-3 py-2 text-white tracking-[0.25em] outline-none focus:border-sky-500"
              />
            </label>

            {error && <p className="text-sm text-red-400">{error}</p>}

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-sky-600 px-3 py-2 font-medium text-white transition hover:bg-sky-500 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSubmitting ? "Verifying..." : "Verify and create account"}
            </button>

            <div className="flex items-center justify-between gap-3">
              <button
                type="button"
                onClick={handleResendCode}
                disabled={isResending || resendCooldownSeconds > 0}
                className="rounded-md border border-slate-700 px-3 py-1.5 text-sm text-slate-200 transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isResending
                  ? "Resending..."
                  : resendCooldownSeconds > 0
                    ? `Resend in ${formatCountdown(resendCooldownSeconds)}`
                    : "Resend code"}
              </button>

              <button
                type="button"
                onClick={handleChangeEmail}
                className="rounded-md border border-slate-700 px-3 py-1.5 text-sm text-slate-200 transition hover:bg-slate-800"
              >
                Change email
              </button>
            </div>
          </form>
        )}
      </section>
    </main>
  )
}
