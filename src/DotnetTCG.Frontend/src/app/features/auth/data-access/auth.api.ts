import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../../../core/http/api-url.token';
import { AuthTokenDto, LoginBody, RegisterBody, UserDto } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_URL);

  register(body: RegisterBody): Observable<UserDto> {
    return this.http.post<UserDto>(`${this.baseUrl}/v1/auth/register`, body);
  }

  login(body: LoginBody): Observable<AuthTokenDto> {
    return this.http.post<AuthTokenDto>(`${this.baseUrl}/v1/auth/login`, body);
  }

  me(): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.baseUrl}/v1/users/me`);
  }

  requestRecovery(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/v1/password-recovery/request`, { email });
  }

  validateRecoveryToken(token: string): Observable<{ valid: boolean }> {
    return this.http.post<{ valid: boolean }>(
      `${this.baseUrl}/v1/password-recovery/validate`, { token });
  }

  resetPassword(token: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/v1/password-recovery/reset`, { token, newPassword });
  }
}
