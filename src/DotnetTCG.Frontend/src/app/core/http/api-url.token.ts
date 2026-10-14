import { InjectionToken } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Base URL del backend (inyectable para testear y reconfigurar). */
export const API_URL = new InjectionToken<string>('API_URL', {
  providedIn: 'root',
  factory: () => environment.apiUrl,
});
