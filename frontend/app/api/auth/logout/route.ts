import { NextResponse } from "next/server"
import { ACCESS_TOKEN_COOKIE_NAME } from "@/lib/server/auth"

export async function POST() {
  const response = NextResponse.json({ ok: true })
  response.cookies.set(ACCESS_TOKEN_COOKIE_NAME, "", {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict",
    path: "/",
    maxAge: 0,
  })
  return response
}
