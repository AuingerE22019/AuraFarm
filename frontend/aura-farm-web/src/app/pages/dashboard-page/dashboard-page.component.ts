import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
})
export class DashboardPageComponent {
  protected readonly endpoints = [
    'addresses',
    'bookings',
    'classes',
    'contracts',
    'discounts',
    'emergencycontacts',
    'equipment',
    'locations',
    'memberemergencycontacts',
    'members',
    'membershiptiers',
    'payments',
    'rooms',
    'sessions',
    'staff',
    'tierprices',
  ];

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
  ) {}

  protected logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
