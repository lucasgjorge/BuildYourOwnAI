// Mirrors the token response of Identity's POST /api/auth/login and /api/auth/refresh.
export type TokenResponse = { tokenType: string; accessToken: string; expiresIn: number; refreshToken: string }

export type Credentials = { email: string; password: string }
