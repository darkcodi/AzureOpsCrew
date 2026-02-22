import { NextResponse } from "next/server"

export async function POST() {
  return NextResponse.json(
    { error: "Password login is disabled. Use Keycloak sign-in." },
    { status: 410 }
  )
}
