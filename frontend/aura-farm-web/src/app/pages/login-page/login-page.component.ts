import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent {
  protected username = signal('admin');
  protected password = signal('');
  protected error = signal<string | null>(null);

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
  ) {}

  protected submit(): void {
    this.error.set(null);
    const ok = this.auth.login(this.username(), this.password());
    if (!ok) {
      this.error.set('Login failed. Use admin / password.');
      return;
    }
    void this.router.navigateByUrl('/');
  }
}
