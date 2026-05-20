import { Routes } from '@angular/router';

import { DashboardPageComponent } from './pages/dashboard-page/dashboard-page.component';
import { LoginPageComponent } from './pages/login-page/login-page.component';
import { StaffLoginComponent } from './pages/staff-login/staff-login';
import { StaffDashboardComponent } from './pages/staff-dashboard/staff-dashboard';
import { MemberDashboardComponent } from './pages/member-dashboard/member-dashboard';
import { rootGuard, staffEntryGuard, memberAuthGuard, staffAuthGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginPageComponent },
  { path: 'staff', canActivate: [staffEntryGuard], children: [] },
  { path: 'staff/login', component: StaffLoginComponent },
  { path: 'staff/dashboard', component: StaffDashboardComponent, canActivate: [staffAuthGuard] },
  { path: 'dashboard', component: MemberDashboardComponent, canActivate: [memberAuthGuard] },
  { path: '', canActivate: [rootGuard], children: [] },
  { path: '**', redirectTo: '' },
];



