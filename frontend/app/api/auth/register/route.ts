import { NextResponse } from "next/server"

export async function POST() {
  return NextResponse.json(
    { error: "Email registration is disabled. Use Keycloak sign-up." },
    { status: 410 }
  )
}
