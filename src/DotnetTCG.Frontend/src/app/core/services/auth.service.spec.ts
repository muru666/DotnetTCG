import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

function fakeJwt(claims: Record<string, unknown>): string {
  const b64 = (o: unknown) => btoa(JSON.stringify(o));
  return `${b64({ alg: 'HS256' })}.${b64(claims)}.sig`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('inicia sin token', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.getToken()).toBeNull();
  });

  it('register envía el payload correcto', () => {
    let ok = false;
    service.register({ username: 'ash', email: 'ash@x.test', password: 'password1' })
      .subscribe({ next: () => (ok = true) });

    const req = httpMock.expectOne(`${environment.apiUrl}/v1/auth/register`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ username: 'ash', email: 'ash@x.test', password: 'password1' });
    req.flush(null);
    expect(ok).toBe(true);
  });

  it('login guarda el token y lo expone', () => {
    const token = fakeJwt({ unique_name: 'ash', sub: 'user-1' });
    service.login('ash', 'password1').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/v1/auth/login`);
    expect(req.request.body).toEqual({ username: 'ash', password: 'password1' });
    req.flush({ accessToken: token, expiresAt: '2030-01-01T00:00:00Z' });

    expect(service.getToken()).toBe(token);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.getUsername()).toBe('ash');
    expect(service.getUserId()).toBe('user-1');
  });

  it('login sin token falla y no se autentica', () => {
    let error!: Error;
    service.login('ash', 'bad').subscribe({ error: e => (error = e) });

    httpMock.expectOne(`${environment.apiUrl}/v1/auth/login`)
      .flush({ accessToken: '   ' });

    expect(error).toBeInstanceOf(Error);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('logout limpia el token', () => {
    const token = fakeJwt({ sub: 'user-1' });
    service.login('ash', 'password1').subscribe();
    httpMock.expectOne(`${environment.apiUrl}/v1/auth/login`)
      .flush({ accessToken: token, expiresAt: '2030-01-01T00:00:00Z' });
    expect(service.isAuthenticated()).toBe(true);

    service.logout();
    expect(service.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('pokemon_tcg_auth_token')).toBeNull();
  });

  it('login con credenciales inválidas muestra mensaje claro', () => {
    let error!: Error;
    service.login('ash', 'bad').subscribe({ error: e => (error = e) });

    httpMock.expectOne(`${environment.apiUrl}/v1/auth/login`)
      .flush({ detail: 'Wrong password' }, { status: 401, statusText: 'Unauthorized' });

    expect(error.message).toContain('incorrectos');
  });

  it('token corrupto no rompe el decodificado', () => {
    sessionStorage.setItem('pokemon_tcg_auth_token', 'not-a-jwt');
    const fresh = TestBed.inject(AuthService);

    expect(fresh.getUsername()).toBeNull();
    expect(fresh.getUserId()).toBeNull();
  });
});
