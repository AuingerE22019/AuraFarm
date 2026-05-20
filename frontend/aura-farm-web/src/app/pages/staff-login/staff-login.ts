import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';

@Component({
  selector: 'app-staff-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <h2>Staff Login</h2>
        <div class="error" *ngIf="error">{{ error }}</div>
        
        <form (ngSubmit)="login()">
          <div class="form-group">
            <label>Email / Username</label>
            <input type="text" [(ngModel)]="email" name="email" required />
          </div>
          
          <div class="form-group">
            <label>Password</label>
            <div class="password-input-wrap">
              <input [type]="showPassword ? 'text' : 'password'" [(ngModel)]="password" name="password" required />
              <button type="button" class="toggle-pwd" (click)="showPassword = !showPassword">
                {{ showPassword ? '🙈' : '👁️' }}
              </button>
            </div>
          </div>
          
          <button type="submit" class="btn-submit" [disabled]="loading">
            {{ loading ? 'Logging in...' : 'Login' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .login-container { display: flex; justify-content: center; align-items: center; height: 100vh; background: var(--bg-page); }
    .login-card { background: var(--surface-1); padding: 2rem; border-radius: var(--radius-panel); border: 1px solid var(--border); box-shadow: var(--shadow-panel); width: 100%; max-width: 400px; color: var(--text); }
    .form-group { margin-bottom: 1rem; }
    .form-group label { display: block; margin-bottom: 0.5rem; font-weight: bold; color: var(--text-secondary); }
    .form-group input { width: 100%; padding: 0.5rem; border: 1px solid var(--border-strong); border-radius: 4px; box-sizing: border-box; background: var(--void-0); color: var(--text); }
    
    .password-input-wrap { display: flex; position: relative; }
    .password-input-wrap input { padding-right: 2.5rem; }
    .toggle-pwd { position: absolute; right: 5px; top: 50%; transform: translateY(-50%); background: none; border: none; padding: 0; width: auto; color: var(--text-muted); cursor: pointer; font-size: 1.2rem; }
    .toggle-pwd:hover { color: var(--text); }

    .btn-submit { width: 100%; padding: 0.75rem; background: var(--accent-cyan); color: #000; font-weight: bold; border: none; border-radius: 4px; cursor: pointer; font-size: 1rem; margin-top: 1rem; }
    .btn-submit:disabled { background: var(--muted); cursor: not-allowed; }
    .error { color: var(--danger-text); background: var(--danger-bg); border: 1px solid var(--danger-border); padding: 0.5rem; border-radius: 4px; margin-bottom: 1rem; }
  `]
})
export class StaffLoginComponent {
  email = '';
  password = '';
  showPassword = false;
  loading = false;
  error = '';

  constructor(private http: HttpClient, private router: Router) {}

  login() {
    this.loading = true;
    this.error = '';
    
    this.http.post<any>('/api/Auth/staff/login', { email: this.email, password: this.password })
      .subscribe({
        next: (res) => {
          localStorage.setItem('token', res.access_token);
          this.router.navigate(['/staff/dashboard']);
        },
        error: (err) => {
          this.loading = false;
          this.error = 'Login failed. Please check your credentials.';
        }
      });
  }
}

