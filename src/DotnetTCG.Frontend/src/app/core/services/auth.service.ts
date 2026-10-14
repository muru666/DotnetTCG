import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

interface LoginResponse {
  accessToken: string;
  expiresAt: string;
}

interface PasswordRecoveryResponse {
  message: string;
}

interface PasswordTokenValidationResponse {
  valid: boolean;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly tokenKey = 'pokemon_tcg_auth_token';
  private readonly apiUrl = environment.apiUrl;
  private readonly tokenSignal = signal<string | null>(this.readStoredToken());

  public readonly isAuthenticated = computed(() => Boolean(this.tokenSignal()));

  constructor(private http: HttpClient) {}

  public register(payload: RegisterRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/v1/auth/register`, payload).pipe(
      catchError(error => throwError(() => this.toFriendlyError(error, 'No pudimos crear tu usuario. Intenta nuevamente.')))
    );
  }

  public login(usernameOrEmail: string, password: string): Observable<void> {
    return this.http
      .post<LoginResponse>(`${this.apiUrl}/v1/auth/login`, { username: usernameOrEmail, password })
      .pipe(
        map(response => response?.accessToken?.trim() ?? ''),
        map(token => {
          if (!token) {
            throw new Error('No recibimos un token valido para iniciar sesion.');
          }

          this.storeToken(token);
        }),
        catchError(error => throwError(() => this.toFriendlyError(error, 'Usuario/Email o contraseña incorrectos.')))
      );
  }

  public requestPasswordRecovery(email: string): Observable<PasswordRecoveryResponse> {
    return this.http.post<PasswordRecoveryResponse>(`${this.apiUrl}/v1/password-recovery/request`, { email }).pipe(
      catchError(error =>
        throwError(() => this.toFriendlyError(error, 'No pudimos enviar el correo de recuperacion. Intenta de nuevo en unos minutos.'))
      )
    );
  }

  public validateRecoveryToken(token: string): Observable<boolean> {
    return this.http
      .post<PasswordTokenValidationResponse>(`${this.apiUrl}/v1/password-recovery/validate`, { token })
      .pipe(
        map(response => response.valid),
        catchError(error => throwError(() => this.toFriendlyError(error, 'El enlace de recuperacion no es valido o ya vencio.')))
      );
  }

  public resetPassword(token: string, newPassword: string): Observable<PasswordRecoveryResponse> {
    return this.http.post<PasswordRecoveryResponse>(`${this.apiUrl}/v1/password-recovery/reset`, { token, newPassword }).pipe(
      catchError(error => throwError(() => this.toFriendlyError(error, 'No pudimos cambiar la contraseña. Intenta con un nuevo enlace.')))
    );
  }

  public getToken(): string | null {
    return this.tokenSignal();
  }

  public getUsername(): string | null {
    const payload = this.decodeToken();
    return (payload?.['unique_name'] as string)
      ?? (payload?.['username'] as string)
      ?? null;
  }

  public getUserId(): string | null {
    const payload = this.decodeToken();
    return (payload?.['sub'] as string) ?? null;
  }

  private decodeToken(): Record<string, unknown> | null {
    const token = this.tokenSignal();
    if (!token) {
      return null;
    }
    try {
      return JSON.parse(atob(token.split('.')[1]));
    } catch {
      return null;
    }
  }

  public logout(): void {
    sessionStorage.removeItem(this.tokenKey);
    this.tokenSignal.set(null);
  }

  private storeToken(token: string): void {
    sessionStorage.setItem(this.tokenKey, token);
    this.tokenSignal.set(token);
  }

  private readStoredToken(): string | null {
    return sessionStorage.getItem(this.tokenKey);
  }

  private toFriendlyError(error: unknown, fallbackMessage: string): Error {
    if (error instanceof Error && error.message) {
      return error;
    }

    return new Error(fallbackMessage);
  }
}