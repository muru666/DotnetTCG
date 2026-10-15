import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { SoundService } from '../services/sound.service';

/**
 * HttpErrorInterceptor - Centralized error handling for all HTTP requests
 */
@Injectable()
export class HttpErrorInterceptor implements HttpInterceptor {
  constructor(
    private router: Router,
    private authService: AuthService,
    private soundService: SoundService
  ) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError((error: HttpErrorResponse) => {
        this.soundService.playError();

        if (error.error instanceof ErrorEvent) {
          console.error('Network error:', request.url);
          return throwError(() => new Error('Hubo un problema de conexion. Verifica tu red e intenta otra vez.'));
        }

        if (error.status === 401) {
          const isPublicAuthRequest =
            request.url.includes('/v1/auth/login') ||
            request.url.includes('/v1/auth/register') ||
            request.url.includes('/v1/password-recovery');

          if (!isPublicAuthRequest) {
            this.authService.logout();
            this.router.navigate(['/iniciar-sesion']);
          }
        }

        console.error('HTTP error:', error.status, request.url, error.error);
        return throwError(() => error);
      })
    );
  }
}
