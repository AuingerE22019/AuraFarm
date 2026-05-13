import { Injectable } from '@angular/core';

const TOKEN_KEY = 'aurafarm_admin_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  isLoggedIn(): boolean {
    return localStorage.getItem(TOKEN_KEY) === '1';
  }

  login(username: string, password: string): boolean {
    const ok = username === 'admin' && password === 'password';
    if (ok) localStorage.setItem(TOKEN_KEY, '1');
    return ok;
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
  }
}

