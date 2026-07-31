import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface LoginResponse {
  token: string;
  refreshToken: string;
  fullName: string;
  role: string;
}

export interface QrStartResponse {
  code: string;
  expiresInSeconds: number;
}

export interface LoginMethods {
  qrEnabled: boolean;
  keyEnabled: boolean;
}

export interface UserInfo {
  userId: number;
  username: string;
  fullName: string;
  roles: string[];
  permissions: string[];
  startPage: string | null;
}

const ACCESS = 'cartex.access';
const REFRESH = 'cartex.refresh';

function decodePayload(token: string): Record<string, unknown> {
  const part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  return JSON.parse(atob(part));
}

function parseToken(token: string): UserInfo {
  const claims = decodePayload(token);
  const many = (key: string) => {
    const value = claims[key];
    return Array.isArray(value) ? (value as string[]) : typeof value === 'string' ? [value] : [];
  };
  return {
    userId: Number(claims['userId'] ?? 0),
    username: String(claims['username'] ?? ''),
    fullName: String(claims['fullName'] ?? claims['username'] ?? ''),
    roles: many('role'),
    permissions: many('permission'),
    startPage: (claims['startPage'] as string) ?? null,
  };
}

function isExpiringSoon(token: string): boolean {
  try {
    const exp = Number(decodePayload(token)['exp']);
    return exp * 1000 <= Date.now() + 60_000;
  } catch {
    return true;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly user = signal<UserInfo | null>(null);
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private persist = false;
  private refreshing: Promise<string | null> | null = null;

  readonly currentUser = this.user.asReadonly();
  readonly isAuthenticated = computed(() => this.user() !== null);

  constructor() {
    this.restore();
  }

  async login(username: string, password: string, rememberMe: boolean): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<LoginResponse>('/api/auth/login', { username, password, deviceName: 'Web' }),
    );
    this.apply(res, rememberMe);
  }

  loginMethods(): Promise<LoginMethods> {
    return firstValueFrom(this.http.get<LoginMethods>('/api/auth/login-methods'));
  }

  startQr(): Promise<QrStartResponse> {
    return firstValueFrom(this.http.post<QrStartResponse>('/api/auth/qr/start', {}));
  }

  async pollQr(code: string): Promise<boolean> {
    const res = await firstValueFrom(
      this.http.post<LoginResponse | null>('/api/auth/qr/poll', { code, deviceName: 'Web' }),
    );
    if (!res) return false;
    this.apply(res, false);
    return true;
  }

  async ensureFreshToken(): Promise<string | null> {
    if (!this.accessToken) return null;
    if (!isExpiringSoon(this.accessToken)) return this.accessToken;
    if (!this.refreshToken) return this.accessToken;
    this.refreshing ??= this.doRefresh();
    try {
      return await this.refreshing;
    } finally {
      this.refreshing = null;
    }
  }

  hasPermission(permission: string): boolean {
    const user = this.user();
    return !!user && (
      user.permissions.includes('*')
      || permission.split('|').some((candidate) => user.permissions.includes(candidate))
    );
  }

  logout(): void {
    if (this.refreshToken)
      this.http.post('/api/auth/logout', { refreshToken: this.refreshToken }).subscribe({ error: () => {} });
    this.accessToken = null;
    this.refreshToken = null;
    this.user.set(null);
    for (const store of [sessionStorage, localStorage]) {
      store.removeItem(ACCESS);
      store.removeItem(REFRESH);
    }
  }

  private async doRefresh(): Promise<string | null> {
    try {
      const res = await firstValueFrom(
        this.http.post<LoginResponse>('/api/auth/refresh', { refreshToken: this.refreshToken, deviceName: 'Web' }),
      );
      this.apply(res, this.persist);
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 401) {
        this.logout();
        this.router.navigate(['/login']);
        return null;
      }
    }
    return this.accessToken;
  }

  async validateSession(): Promise<void> {
    if (!this.refreshToken) return;
    this.refreshing ??= this.doRefresh();
    try {
      await this.refreshing;
    } finally {
      this.refreshing = null;
    }
  }

  private apply(res: LoginResponse, persist: boolean): void {
    this.accessToken = res.token;
    this.refreshToken = res.refreshToken;
    this.persist = persist;
    this.user.set(parseToken(res.token));
    const store = persist ? localStorage : sessionStorage;
    const other = persist ? sessionStorage : localStorage;
    store.setItem(ACCESS, res.token);
    store.setItem(REFRESH, res.refreshToken);
    other.removeItem(ACCESS);
    other.removeItem(REFRESH);
  }

  private restore(): void {
    for (const store of [sessionStorage, localStorage]) {
      const access = store.getItem(ACCESS);
      const refresh = store.getItem(REFRESH);
      if (access && refresh) {
        this.accessToken = access;
        this.refreshToken = refresh;
        this.persist = store === localStorage;
        try {
          this.user.set(parseToken(access));
        } catch {
          this.logout();
        }
        return;
      }
    }
  }
}
