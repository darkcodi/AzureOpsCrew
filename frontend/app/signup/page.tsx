import { redirect } from "next/navigation"
import { getKeycloakAuthFeatureConfig } from "@/lib/server/keycloak"

export default function SignupPage() {
  const features = getKeycloakAuthFeatureConfig()
  if (!features.localSignupEnabled) {
    redirect("/login")
  }
  redirect("/api/auth/keycloak/start?mode=signup")
}
