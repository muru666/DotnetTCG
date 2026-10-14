/** Contratos del backend (POST /v1/auth/*, /v1/users/me, /v1/password-recovery/*). */

export interface UserDto {
  readonly id: string;
  readonly username: string;
  readonly email: string;
  readonly role: string;
}

export interface AuthTokenDto {
  readonly accessToken: string;
  readonly expiresAt: string;
}

export interface RegisterBody {
  readonly username: string;
  readonly email: string;
  readonly password: string;
}

export interface LoginBody {
  readonly username: string;
  readonly password: string;
}
