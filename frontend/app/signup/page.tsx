import { redirect } from "next/navigation"
import { AuthStartRedirect } from "@/components/auth-start-redirect"
import { getKeycloakAuthFeatureConfig } from "@/lib/server/keycloak"

export default function SignupPage() {
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
