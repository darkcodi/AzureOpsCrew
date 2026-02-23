import { unstable_noStore as noStore } from "next/cache"
import { redirect } from "next/navigation"
import { AuthStartRedirect } from "@/components/auth-start-redirect"
import { getKeycloakAuthFeatureConfig } from "@/lib/server/keycloak"

export const dynamic = "force-dynamic"

export default function SignupPage() {
  noStore()
  const features = getKeycloakAuthFeatureConfig()
  if (!features.localSignupEnabled) {
    redirect("/login?error=Sign%20up%20is%20disabled")
  }

  return (
    <AuthStartRedirect
      href="/api/auth/keycloak/start?mode=signup"
      title="Redirecting to sign up"
      description="Opening secure registration..."
    />
  )
}
