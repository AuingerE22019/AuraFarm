import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

/** Decode JWT payload (no crypto verification — server validates). */
function jwtPayload(): Record<string, unknown> | null {
  const t = localStorage.getItem('token');
  if (!t) return null;
  const parts = t.split('.');
  if (parts.length < 2) return null;
  try {
    const b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = atob(b64);
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

function jwtRoles(): string[] {
  const p = jwtPayload();
  if (!p) return [];
  const claim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
  const r = p[claim] ?? p['role'];
  if (r == null) return [];
  return Array.isArray(r) ? (r as string[]) : [String(r)];
}

export const memberAuthGuard: CanActivateFn = () => {
  const router = inject(Router);
  if (!localStorage.getItem('token')) return router.parseUrl('/login');
  if (jwtRoles().includes('member')) return true;
  return router.parseUrl('/staff/login');
};

export const staffAuthGuard: CanActivateFn = () => {
  const router = inject(Router);
  if (!localStorage.getItem('token')) return router.parseUrl('/staff/login');
  if (jwtRoles().includes('member')) return router.parseUrl('/dashboard');
  return true;
};

export const rootGuard: CanActivateFn = () => {
  const router = inject(Router);
  if (!localStorage.getItem('token')) return router.parseUrl('/login');
  if (jwtRoles().includes('member')) return router.parseUrl('/dashboard');
  return router.parseUrl('/staff/dashboard');
};

export const staffEntryGuard: CanActivateFn = () => {
  const router = inject(Router);
  if (!localStorage.getItem('token')) return true;
  if (jwtRoles().includes('member')) return router.parseUrl('/dashboard');
  return router.parseUrl('/staff/dashboard');
};
