import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { finalize, TimeoutError, timeout } from 'rxjs';

interface MemberProfile {
  memberId: string;
  firstName: string | null;
  lastName: string | null;
  username: string | null;
  email: string | null;
  phone: string | null;
  dateOfBirth: string | null;
  isVerifiedStudent: boolean | null;
  registrationDate: string | null;
  addressId: string | null;
}

interface EmergencyRow {
  contactId: string;
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  email: string | null;
  relation: string | null;
  priority: number | null;
}

interface SubscriptionDto {
  hasContract: boolean;
  contractId?: string;
  status?: string;
  phase?: string;
  phaseLabel?: string;
  startDate?: string;
  commitmentEndDate?: string;
  firstFullBillingDate?: string;
  monthlyRate?: number;
  currency?: string;
  billingCycle?: string;
  billingCycleLabel?: string;
  autoRenew?: boolean;
  renewalLabel?: string;
  cancelledAt?: string | null;
  pauseEffectiveDate?: string | null;
  resumeEffectiveDate?: string | null;
  activeThroughDate?: string | null;
  nextBillingDate?: string;
  lastPaymentAmount?: number | null;
  lastPaymentPeriodEnd?: string | null;
  tierName?: string | null;
  accessLevel?: string | null;
  renewalTierName?: string | null;
  renewalAccessLevel?: string | null;
  renewalMonthlyRate?: number | null;
  renewalBillingCycleLabel?: string | null;
  renewalAddons?: { addonName: string | null; monthlyRate: number }[];
  renewalEffectiveFrom?: string;
  renewalPlanChanged?: boolean;
  showRenewalPlan?: boolean;
  renewalPlan?: {
    effectiveFrom: string;
    effectiveFromLabel: string;
    title: string;
    configurationLabel: string;
    customized: boolean;
    configuredAt?: string | null;
    homeLocation?: string | null;
    autoRenew: boolean;
    tierName: string | null;
    accessLevel: string | null;
    accessLevelLabel: string | null;
    billingCycle: string | null;
    billingCycleLabel: string | null;
    baseMonthlyRate: number | null;
    addonsMonthlyTotal: number;
    monthlyRate: number | null;
    addons: { addonName: string | null; monthlyRate: number }[];
    breakdown: { label: string; value: string; emphasis?: boolean }[];
    applies: boolean;
    changed: boolean;
  };
  homeLocation?: string | null;
  addons?: { addonName: string | null; monthlyRate: number }[];
  canPause?: boolean;
  canResume?: boolean;
  canUndoCancel?: boolean;
  canCancel?: boolean;
  canChangeRenewal?: boolean;
}

interface RenewalTierRow {
  tierPriceId: string;
  billingCycle: string;
  amount: number;
  tierName: string | null;
  accessLevel: string;
}

interface RenewalAddonRow {
  addonPriceId: string;
  addonId: string;
  addonName: string;
  billingCycle: string;
  amount: number;
  isCombo: boolean;
  includesSauna?: boolean;
  includesSolarium?: boolean;
  includesDrinks?: boolean;
  includesCoffee?: boolean;
}

interface RenewalPlanCatalog {
  tierPrices: RenewalTierRow[];
  addonPrices: RenewalAddonRow[];
  isVerifiedStudent?: boolean;
  current: {
    tierPriceId: string | null;
    addonPriceIds: string[];
    billingCycle: string;
    accessLevel: string;
  };
}

interface AddonFeatureFlags {
  sauna: boolean;
  solarium: boolean;
  drinks: boolean;
  coffee: boolean;
}

interface RenewalAddonPackageView {
  addonId: string;
  addonName: string;
  isCombo: boolean;
  isAllIn: boolean;
  includesSauna: boolean;
  includesSolarium: boolean;
  includesDrinks: boolean;
  includesCoffee: boolean;
  priceMonthly: number;
  priceAnnual: number;
}

@Component({
  selector: 'app-member-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="dashboard">
      <header>
        <div>
          <h1>Mein Dashboard</h1>
          <p class="sub" *ngIf="profile">
            {{ profile.firstName }} {{ profile.lastName }}
            <span *ngIf="profile.email">· {{ profile.email }}</span>
          </p>
        </div>
        <button type="button" (click)="logout()" class="btn-logout">Logout</button>
      </header>

      <div class="page-loading" *ngIf="pageLoading" aria-live="polite">
        <div class="spinner"></div>
        <p>Lade Dashboard …</p>
      </div>

      <main *ngIf="profile">
        <section class="panel">
          <h2>Profil</h2>
          <dl class="dl-grid">
            <dt>Username</dt>
            <dd>{{ profile.username ?? '—' }}</dd>
            <dt>Telefon</dt>
            <dd>{{ profile.phone ?? '—' }}</dd>
            <dt>Geburtsdatum</dt>
            <dd>{{ profile.dateOfBirth ?? '—' }}</dd>
            <dt>Student</dt>
            <dd>{{ profile.isVerifiedStudent ? 'Ja' : 'Nein' }}</dd>
          </dl>
        </section>

        <section class="panel">
          <div class="panel-head">
            <h2>Notfallkontakte</h2>
            <button type="button" class="btn-primary" (click)="openEcModal()">Notfallkontakt hinzufügen</button>
          </div>
          <p class="muted">
            Notfallkontakte können nur hier im Mitglieder-Dashboard verwaltet werden — nicht bei der Staff-Registrierung.
          </p>

          <p *ngIf="!emergencyList.length && !ecLoading">Noch keine Notfallkontakte.</p>
          <div *ngIf="ecLoading">Lade …</div>

          <table *ngIf="emergencyList.length">
            <thead>
              <tr>
                <th>Name</th>
                <th>Telefon</th>
                <th>E-Mail</th>
                <th>Beziehung</th>
                <th>Prio</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let r of emergencyList">
                <td>{{ r.firstName }} {{ r.lastName }}</td>
                <td>{{ r.phoneNumber }}</td>
                <td>{{ r.email ?? '—' }}</td>
                <td>{{ r.relation ?? '—' }}</td>
                <td>{{ r.priority ?? '—' }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="panel" *ngIf="subscription?.hasContract">
          <h2>Mein Abo</h2>
          <p class="status-badge" [class.warn]="subscription?.phase === 'cancelled_running'">{{ subscription?.phaseLabel }}</p>

          <dl class="dl-grid">
            <dt>Modell</dt>
            <dd>{{ subscription?.tierName ?? '—' }}</dd>
            <dt>Standort</dt>
            <dd>{{ subscription?.homeLocation ?? '—' }}</dd>
            <dt>Monatsbeitrag</dt>
            <dd>{{ formatMoney(subscription?.monthlyRate ?? 0) }} / Monat</dd>
            <dt>Vertragsbeginn</dt>
            <dd>{{ formatDate(subscription?.startDate) }}</dd>
            <dt>Laufzeit</dt>
            <dd>{{ subscription?.billingCycleLabel }}</dd>
            <dt>Vertrag bis</dt>
            <dd>{{ formatDate(subscription?.commitmentEndDate) }}</dd>
            <dt>Erster voller Monat (1.)</dt>
            <dd>{{ formatDate(subscription?.firstFullBillingDate) }}</dd>
            <dt>Nächster Zahltag (1.)</dt>
            <dd>{{ formatDate(subscription?.nextBillingDate) }}</dd>
            <dt *ngIf="subscription?.activeThroughDate">Aktiv bis</dt>
            <dd *ngIf="subscription?.activeThroughDate">{{ formatDate(subscription?.activeThroughDate) }}</dd>
            <dt>Letzte Zahlung</dt>
            <dd>
              <ng-container *ngIf="subscription?.lastPaymentAmount != null">
                {{ formatMoney(subscription!.lastPaymentAmount!) }}
                <span class="muted">(bis {{ formatDate(subscription?.lastPaymentPeriodEnd) }})</span>
              </ng-container>
              <ng-container *ngIf="subscription?.lastPaymentAmount == null">—</ng-container>
            </dd>
            <dt>Nach Laufzeitende</dt>
            <dd>{{ subscription?.renewalLabel ?? '—' }}</dd>
          </dl>

          <ng-container *ngIf="subscription?.showRenewalPlan && subscription?.renewalPlan as rp">
            <div class="renewal-plan-box">
              <h3 class="renewal-plan-title">{{ rp.title }}</h3>
              <p class="renewal-config-label">{{ rp.configurationLabel }}</p>
              <p class="renewal-effective">{{ rp.effectiveFromLabel }}</p>
              <p class="renewal-hint muted" *ngIf="rp.applies">
                Aktuelles Abo läuft bis {{ formatDate(subscription?.commitmentEndDate) }} — danach gilt diese Konfiguration.
              </p>
              <p class="renewal-warn" *ngIf="!rp.applies && subscription?.phase === 'cancelled_running'">
                Vertrag ist gekündigt — dieses Folge-Abo wird nicht angewendet.
              </p>
              <p class="renewal-warn" *ngIf="!rp.applies && !subscription?.autoRenew">
                Automatische Verlängerung ist aus — Folge-Abo wird nicht angewendet.
              </p>

              <h4 class="renewal-section-title">Konfiguration</h4>
              <dl class="dl-grid renewal-plan-grid">
                <dt>Standort</dt>
                <dd>{{ rp.homeLocation ?? subscription?.homeLocation ?? '—' }}</dd>
                <dt>Abo-Modell</dt>
                <dd>
                  {{ rp.accessLevelLabel ?? renewalTierTitle(rp.accessLevel) }}
                  <span class="muted" *ngIf="rp.tierName">({{ rp.tierName }})</span>
                </dd>
                <dt>Laufzeit</dt>
                <dd>{{ rp.billingCycleLabel ?? '—' }}</dd>
                <dt>Automatische Verlängerung</dt>
                <dd>{{ rp.autoRenew ? 'Ja' : 'Nein' }}</dd>
                <dt>Zusatzpakete</dt>
                <dd>
                  <ng-container *ngIf="rp.addons?.length; else noRenewalAddons">
                    <ul class="renewal-addon-list">
                      <li *ngFor="let a of rp.addons">
                        {{ a.addonName }} — {{ formatMoney(a.monthlyRate) }} / Monat
                      </li>
                    </ul>
                  </ng-container>
                  <ng-template #noRenewalAddons>Keine Zusatzpakete</ng-template>
                </dd>
                <dt *ngIf="rp.configuredAt">Zuletzt angepasst</dt>
                <dd *ngIf="rp.configuredAt">{{ formatDateTime(rp.configuredAt) }}</dd>
              </dl>

              <h4 class="renewal-section-title">Kostenübersicht</h4>
              <ul class="renewal-breakdown">
                <li *ngFor="let line of rp.breakdown" [class.emphasis]="line.emphasis">
                  <span>{{ line.label }}</span>
                  <span>{{ line.value }}</span>
                </li>
              </ul>
            </div>
          </ng-container>

          <div *ngIf="subscription?.addons?.length" class="addon-chips">
            <span class="chip" *ngFor="let a of subscription?.addons">{{ a.addonName }} (+{{ formatMoney(a.monthlyRate) }})</span>
          </div>

          <p class="muted">
            Zahltag ist immer der <strong>1.</strong> Pause ist jederzeit bis Vertragsende möglich.
            <strong>Reaktivieren</strong> nimmt eine Kündigung zurück oder startet das Abo nach Pause wieder.
            <strong>Abo ändern</strong> nur ohne aktive Kündigung (oder nach Reaktivieren).
          </p>
          <div class="error" *ngIf="subActionError">{{ subActionError }}</div>
          <div class="success" *ngIf="subActionMsg">{{ subActionMsg }}</div>

          <div class="action-row">
            <button type="button" class="btn-secondary" *ngIf="subscription?.canPause" (click)="pauseSub()" [disabled]="subBusy">Pause planen</button>
            <button type="button" class="btn-secondary" *ngIf="subscription?.canResume" (click)="resumeSub()" [disabled]="subBusy">
              {{
                subscription?.canUndoCancel
                  ? 'Kündigung zurücknehmen'
                  : subscription?.phase === 'pause_scheduled'
                    ? 'Pause abbrechen'
                    : 'Reaktivieren'
              }}
            </button>
            <button type="button" class="btn-secondary" *ngIf="subscription?.canChangeRenewal" (click)="openRenewalModal()" [disabled]="subBusy">Abo nach Laufzeit ändern</button>
            <button type="button" class="btn-danger" *ngIf="subscription?.canCancel" (click)="cancelSub()" [disabled]="subBusy">Kündigen</button>
          </div>
        </section>

        <section class="panel" *ngIf="subscription && !subscription.hasContract && !subLoading">
          <h2>Mein Abo</h2>
          <p class="muted">Kein aktiver Vertrag gefunden.</p>
        </section>

        <section class="panel">
          <h2>Meine Kurse</h2>
          <p class="muted">Deine nächsten eingetragenen Termine.</p>
          <div *ngIf="bookingsLoading" class="muted">Lade …</div>
          <p *ngIf="!bookingsLoading && !myBookings.length" class="muted">Noch keine Anmeldungen.</p>
          <table *ngIf="myBookings.length">
            <thead>
              <tr>
                <th>Kurs</th>
                <th>Termin</th>
                <th>Ort</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let b of myBookings">
                <td>{{ b.classTitle }}</td>
                <td>{{ formatSessionWhen(b.startTime) }}</td>
                <td>{{ b.locationName }} · {{ b.roomName }}</td>
                <td>
                  <button type="button" class="btn-secondary btn-small" (click)="cancelBooking(b.bookingId)">Abmelden</button>
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="panel">
          <h2>Kurse buchen</h2>
          <p class="muted">Verfügbare Termine an deinen Standorten (je nach Abo).</p>
          <div *ngIf="sessionsLoading" class="muted">Lade …</div>
          <div class="error" *ngIf="sessionsError">{{ sessionsError }}</div>
          <div class="success" *ngIf="sessionsMsg">{{ sessionsMsg }}</div>
          <table *ngIf="upcomingSessions.length">
            <thead>
              <tr>
                <th>Kurs</th>
                <th>Termin</th>
                <th>Ort</th>
                <th>Trainer</th>
                <th>Frei</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let s of upcomingSessions">
                <td>{{ s.classTitle }} <span class="muted small">({{ s.difficulty }})</span></td>
                <td>{{ formatSessionWhen(s.startTime) }}</td>
                <td>{{ s.locationName }} · {{ s.roomName }}</td>
                <td>{{ s.trainerName }}</td>
                <td>{{ s.spotsLeft }}</td>
                <td>
                  <span class="chip" *ngIf="s.isBookedByMe">Eingetragen</span>
                  <button
                    type="button"
                    class="btn-primary btn-small"
                    *ngIf="!s.isBookedByMe && s.spotsLeft > 0"
                    (click)="bookSession(s.sessionId)"
                    [disabled]="sessionsBusy"
                  >
                    Anmelden
                  </button>
                  <span class="muted" *ngIf="!s.isBookedByMe && s.spotsLeft === 0">Voll</span>
                </td>
              </tr>
            </tbody>
          </table>
          <p *ngIf="!sessionsLoading && !upcomingSessions.length" class="muted">Keine buchbaren Termine.</p>
        </section>
      </main>

      <main *ngIf="!profile">
        <section class="panel">
          <h2>Profil</h2>
          <div *ngIf="profileLoading">Lade Profil …</div>
          <div class="error" *ngIf="profileError">{{ profileError }}</div>
          <p class="muted" *ngIf="!profileLoading && !profileError">
            Kein Profil geladen. Bitte erneut einloggen.
          </p>
        </section>
      </main>

      <div class="modal-overlay" *ngIf="renewalModalOpen">
        <div class="modal modal-wide">
          <h2>Abo nach Vertragsende planen</h2>
          <p class="muted">
            Wie beim Check-in: Modell, Pakete, Laufzeit —
            <ng-container *ngIf="subscription?.renewalEffectiveFrom; else endOfTerm">
              gültig ab {{ formatDate(subscription?.renewalEffectiveFrom) }}.
            </ng-container>
            <ng-template #endOfTerm>gültig ab dem Tag nach Vertragsende.</ng-template>
            Nur wenn der Vertrag <strong>nicht gekündigt</strong> ist (sonst zuerst „Kündigung zurücknehmen“).
          </p>

          <div *ngIf="renewalLoading" class="muted">Lade Optionen …</div>

          <ng-container *ngIf="!renewalLoading && renewalCatalog">
            <div *ngIf="renewalStep === 'tier'" class="wizard-block">
              <h3 class="wiz-title">1. Abo-Modell</h3>
              <p class="muted small">Alle Preise als Monatsbeitrag.</p>
              <div class="tier-grid">
                <button
                  type="button"
                  class="tier-card"
                  *ngFor="let opt of renewalTierOptions"
                  [class.selected]="renewalAccessLevel === opt.level"
                  (click)="renewalAccessLevel = opt.level"
                >
                  <div class="tier-title">{{ opt.title }}</div>
                  <div class="tier-sub">{{ opt.subtitle }}</div>
                  <div class="tier-range">ca. {{ formatMoney(opt.min) }} – {{ formatMoney(opt.max) }} / Monat</div>
                </button>
              </div>
              <div class="modal-actions">
                <button type="button" class="btn-secondary" (click)="closeRenewalModal()">Abbrechen</button>
                <button type="button" class="btn-primary" (click)="renewalGoAddons()" [disabled]="!renewalAccessLevel">Weiter</button>
              </div>
            </div>

            <div *ngIf="renewalStep === 'addons'" class="wizard-block">
              <h3 class="wiz-title">2. Zusatzpakete</h3>
              <div class="form-group">
                <label>Pakete?</label>
                <select [(ngModel)]="renewalWantAddons" name="rWantAddons" (ngModelChange)="onRenewalWantAddonsChange($event)">
                  <option [ngValue]="false">Nein, nur Basis-Abo</option>
                  <option [ngValue]="true">Ja, Zusatzpakete</option>
                </select>
              </div>
              <div *ngIf="renewalWantAddons" class="addon-list">
                <label class="addon" *ngFor="let p of renewalUniquePackages()" [class.addon-disabled]="renewalAddonDisabled(p)">
                  <input
                    type="checkbox"
                    [checked]="renewalSelectedAddonIds.includes(p.addonId)"
                    [disabled]="renewalAddonDisabled(p)"
                    (change)="renewalToggleAddon(p, $any($event.target).checked)"
                  />
                  <span>
                    <strong>{{ p.addonName }}</strong>
                    <span *ngIf="p.isAllIn" class="tag tag-allin">All In</span>
                    <span *ngIf="p.isCombo && !p.isAllIn" class="tag">Bundle</span>
                    <div class="muted small">
                      {{ formatMoney(renewalAddonFlex(p)) }} / Monat (flexibel) ·
                      {{ formatMoney(renewalAddonCommit(p)) }} / Monat (12 Mon.)
                    </div>
                  </span>
                </label>
              </div>
              <div class="modal-actions">
                <button type="button" class="btn-secondary" (click)="renewalStep = 'tier'">Zurück</button>
                <button type="button" class="btn-primary" (click)="renewalStep = 'billing'">Weiter</button>
              </div>
            </div>

            <div *ngIf="renewalStep === 'billing'" class="wizard-block">
              <h3 class="wiz-title">3. Laufzeit</h3>
              <div class="form-group">
                <label>Vertragslaufzeit</label>
                <select [(ngModel)]="renewalBillingCycle" name="rCycle">
                  <option value="monthly">Flexibel (monatlich kündbar)</option>
                  <option value="annually">12 Monate Mindestlaufzeit</option>
                </select>
              </div>
              <div class="preview-box">
                <div><strong>Monatsbeitrag:</strong> {{ formatMoney(renewalPreviewTotal()) }} / Monat</div>
              </div>
              <div class="modal-actions">
                <button type="button" class="btn-secondary" (click)="renewalStep = 'addons'">Zurück</button>
                <button type="button" class="btn-primary" (click)="renewalStep = 'summary'">Weiter</button>
              </div>
            </div>

            <div *ngIf="renewalStep === 'summary'" class="wizard-block">
              <h3 class="wiz-title">4. Übersicht</h3>
              <ul class="sum-list">
                <li><span>Modell</span><span>{{ renewalTierTitle(renewalAccessLevel) }}</span></li>
                <li><span>Laufzeit</span><span>{{ renewalBillingCycle === 'monthly' ? 'Flexibel' : '12 Monate' }}</span></li>
                <li><span>Monatsbeitrag</span><span>{{ formatMoney(renewalPreviewTotal()) }}</span></li>
              </ul>
              <label class="check-row">
                <input type="checkbox" [(ngModel)]="renewalForm.autoRenew" name="renewAuto" />
                Automatisch verlängern (sonst endet der Vertrag am Laufzeitende)
              </label>
              <div class="error" *ngIf="renewalError">{{ renewalError }}</div>
              <div class="modal-actions">
                <button type="button" class="btn-secondary" (click)="renewalStep = 'billing'">Zurück</button>
                <button type="button" class="btn-primary" (click)="saveRenewal()" [disabled]="renewalSaving">Speichern</button>
              </div>
            </div>
          </ng-container>
        </div>
      </div>

      <div class="modal-overlay" *ngIf="ecModalOpen">
        <div class="modal">
          <h2>Notfallkontakt hinzufügen</h2>
          <form (ngSubmit)="submitEmergency()">
            <div class="form-grid">
              <div class="form-group">
                <label>Vorname *</label>
                <input type="text" name="efn" [(ngModel)]="ecForm.firstName" required />
              </div>
              <div class="form-group">
                <label>Nachname</label>
                <input type="text" name="eln" [(ngModel)]="ecForm.lastName" />
              </div>
              <div class="form-group">
                <label>Telefon *</label>
                <input type="text" name="ephone" [(ngModel)]="ecForm.phoneNumber" required />
              </div>
              <div class="form-group">
                <label>E-Mail</label>
                <input type="email" name="eemail" [(ngModel)]="ecForm.email" />
              </div>
              <div class="form-group">
                <label>Beziehung</label>
                <input type="text" name="erel" [(ngModel)]="ecForm.relation" placeholder="z. B. Mutter" />
              </div>
              <div class="form-group">
                <label>Priorität</label>
                <input type="number" name="epri" [(ngModel)]="ecForm.priority" min="1" />
              </div>
            </div>
            <div class="error" *ngIf="ecError">{{ ecError }}</div>
            <div class="modal-actions">
              <button type="button" class="btn-secondary" (click)="closeEcModal()">Abbrechen</button>
              <button type="submit" class="btn-primary" [disabled]="ecSaving">Speichern</button>
            </div>
          </form>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .dashboard {
        padding: 2rem;
        max-width: 960px;
        margin: 0 auto;
        color: var(--text);
      }
      header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        margin-bottom: 2rem;
        gap: 1rem;
      }
      h1 {
        margin: 0;
      }
      .sub {
        margin: 0.35rem 0 0;
        color: var(--text-secondary);
      }
      .btn-logout {
        background: transparent;
        color: var(--danger-text);
        border: 1px solid var(--danger-border);
        padding: 0.5rem 1rem;
        border-radius: 4px;
        cursor: pointer;
        font-weight: bold;
      }
      .panel {
        background: var(--surface-1);
        padding: 1.5rem;
        border-radius: var(--radius-panel);
        border: 1px solid var(--border);
        box-shadow: var(--shadow-panel);
        margin-bottom: 1.5rem;
      }
      .page-loading {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.75rem 1rem;
        background: var(--surface-1);
        border: 1px solid var(--border);
        border-radius: var(--radius-panel);
        box-shadow: var(--shadow-panel);
        margin-bottom: 1rem;
        color: var(--text-secondary);
      }
      .spinner {
        border: 4px solid var(--surface-3);
        border-top: 4px solid var(--accent-cyan);
        border-radius: 50%;
        width: 18px;
        height: 18px;
        animation: spin 1s linear infinite;
      }
      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
      .panel-head {
        display: flex;
        flex-wrap: wrap;
        justify-content: space-between;
        align-items: center;
        gap: 1rem;
        margin-bottom: 0.75rem;
      }
      .panel h2 {
        margin: 0 0 0.5rem;
      }
      .muted {
        color: var(--text-secondary);
        font-size: 0.9rem;
      }
      .muted-block {
        color: var(--text-secondary);
      }
      .dl-grid {
        display: grid;
        grid-template-columns: 140px 1fr;
        gap: 0.35rem 1rem;
        margin: 0;
      }
      dt {
        font-weight: 600;
        color: var(--text-muted);
      }
      dd {
        margin: 0;
      }
      table {
        width: 100%;
        border-collapse: collapse;
        margin-top: 1rem;
      }
      th,
      td {
        padding: 0.65rem;
        text-align: left;
        border-bottom: 1px solid var(--border);
      }
      th {
        color: var(--text-secondary);
        font-size: 0.85rem;
      }
      .btn-primary {
        padding: 0.5rem 1rem;
        border: none;
        border-radius: 4px;
        cursor: pointer;
        font-weight: bold;
        background: var(--accent-cyan);
        color: #000;
      }
      .btn-secondary {
        padding: 0.5rem 1rem;
        border-radius: 4px;
        cursor: pointer;
        font-weight: bold;
        background: var(--surface-3);
        color: var(--text);
        border: 1px solid var(--border);
      }
      .modal-overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.65);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
        padding: 1rem;
      }
      .modal {
        background: var(--surface-1);
        border: 1px solid var(--border);
        border-radius: var(--radius-panel);
        padding: 1.5rem;
        width: 100%;
        max-width: 520px;
        max-height: 90vh;
        overflow-y: auto;
      }
      .modal-wide {
        max-width: 720px;
      }
      .wiz-title {
        margin: 0 0 0.5rem;
        font-size: 1rem;
        color: var(--text-secondary);
      }
      .tier-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: 0.75rem;
        margin: 0.75rem 0 1rem;
      }
      .tier-card {
        text-align: left;
        border-radius: 12px;
        border: 1px solid var(--border);
        background: rgba(15, 23, 42, 0.45);
        color: var(--text);
        padding: 1rem;
        cursor: pointer;
      }
      .tier-card.selected {
        border-color: var(--border-accent);
        box-shadow: 0 0 0 1px rgba(34, 211, 238, 0.25);
      }
      .tier-title {
        font-weight: 800;
      }
      .tier-sub {
        color: var(--text-secondary);
        font-size: 0.85rem;
        margin: 0.35rem 0;
      }
      .tier-range {
        font-weight: 700;
        color: var(--accent-cyan);
        font-size: 0.9rem;
      }
      .addon-list {
        display: grid;
        gap: 0.5rem;
        margin: 0.5rem 0 1rem;
      }
      .addon {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        padding: 0.5rem;
        border: 1px solid var(--border);
        border-radius: 8px;
      }
      .addon-disabled {
        opacity: 0.55;
      }
      .tag {
        display: inline-block;
        margin-left: 0.35rem;
        padding: 0.1rem 0.45rem;
        border-radius: 999px;
        border: 1px solid rgba(167, 139, 250, 0.35);
        font-size: 0.75rem;
        color: var(--accent-violet);
      }
      .tag-allin {
        border-color: rgba(34, 211, 238, 0.45);
        color: var(--accent-cyan);
      }
      .preview-box {
        padding: 0.75rem;
        border: 1px solid var(--border);
        border-radius: 8px;
        margin-bottom: 1rem;
      }
      .sum-list {
        list-style: none;
        padding: 0;
        margin: 0 0 1rem;
      }
      .sum-list li {
        display: flex;
        justify-content: space-between;
        padding: 0.35rem 0;
        border-bottom: 1px solid rgba(148, 163, 184, 0.12);
      }
      .small {
        font-size: 0.82rem;
      }
      .form-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 0.75rem;
      }
      .form-group label {
        display: block;
        margin-bottom: 0.25rem;
        font-weight: 600;
        font-size: 0.85rem;
        color: var(--text-secondary);
      }
      .form-group input {
        width: 100%;
        padding: 0.45rem;
        box-sizing: border-box;
        border-radius: 4px;
        border: 1px solid var(--border-strong);
        background: var(--void-0);
        color: var(--text);
      }
      .modal-actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 1rem;
      }
      .error {
        color: var(--danger-text);
        margin-top: 0.75rem;
      }
      .success {
        color: var(--accent-teal);
        margin-top: 0.75rem;
      }
      .status-badge {
        display: inline-block;
        padding: 0.25rem 0.65rem;
        border-radius: 999px;
        background: rgba(34, 211, 238, 0.15);
        color: var(--accent-cyan);
        font-weight: 700;
        margin-bottom: 1rem;
      }
      .status-badge.warn {
        background: rgba(251, 191, 36, 0.15);
        color: #fbbf24;
      }
      .action-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        margin-top: 1rem;
      }
      .btn-danger {
        padding: 0.5rem 1rem;
        border-radius: 4px;
        cursor: pointer;
        font-weight: bold;
        background: transparent;
        color: var(--danger-text);
        border: 1px solid var(--danger-border);
      }
      .addon-chips {
        display: flex;
        flex-wrap: wrap;
        gap: 0.35rem;
        margin: 0.75rem 0;
      }
      .chip {
        font-size: 0.82rem;
        padding: 0.2rem 0.5rem;
        border-radius: 999px;
        border: 1px solid var(--border);
        color: var(--text-secondary);
      }
      .check-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin: 0.75rem 0;
        font-size: 0.9rem;
      }
      .renewal-plan-box {
        margin-top: 1.25rem;
        padding: 1rem 1.1rem;
        border-radius: 10px;
        border: 1px solid rgba(34, 211, 238, 0.35);
        background: rgba(34, 211, 238, 0.06);
      }
      .renewal-plan-title {
        margin: 0 0 0.35rem;
        font-size: 1rem;
        color: var(--accent-cyan);
      }
      .renewal-config-label {
        margin: 0 0 0.35rem;
        font-size: 0.9rem;
        color: var(--text-secondary);
      }
      .renewal-section-title {
        margin: 1rem 0 0.5rem;
        font-size: 0.85rem;
        text-transform: uppercase;
        letter-spacing: 0.04em;
        color: var(--text-muted);
      }
      .renewal-addon-list {
        margin: 0;
        padding-left: 1.1rem;
      }
      .renewal-addon-list li {
        margin: 0.2rem 0;
      }
      .renewal-breakdown {
        list-style: none;
        margin: 0;
        padding: 0;
      }
      .renewal-breakdown li {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        padding: 0.4rem 0;
        border-bottom: 1px solid rgba(148, 163, 184, 0.15);
        font-size: 0.92rem;
      }
      .renewal-breakdown li.emphasis {
        font-weight: 800;
        color: var(--accent-cyan);
        border-bottom: none;
        padding-top: 0.55rem;
      }
      .renewal-effective {
        margin: 0 0 0.5rem;
        font-weight: 700;
        color: var(--text);
      }
      .renewal-hint {
        margin: 0 0 0.75rem;
      }
      .renewal-warn {
        margin: 0 0 0.75rem;
        color: #fbbf24;
        font-size: 0.9rem;
      }
      .renewal-plan-grid {
        margin-top: 0.5rem;
      }
      .btn-small {
        padding: 0.35rem 0.65rem;
        font-size: 0.85rem;
      }
      .small {
        font-size: 0.82rem;
      }
    `,
  ],
})
export class MemberDashboardComponent implements OnInit {
  profile: MemberProfile | null = null;
  profileLoading = false;
  profileError = '';
  pageLoading = true;
  private pendingFirstLoads = 3;
  subscription: SubscriptionDto | null = null;
  subLoading = false;
  subBusy = false;
  subActionError = '';
  subActionMsg = '';
  renewalModalOpen = false;
  renewalSaving = false;
  renewalLoading = false;
  renewalError = '';
  renewalStep: 'tier' | 'addons' | 'billing' | 'summary' = 'tier';
  renewalCatalog: RenewalPlanCatalog | null = null;
  renewalAccessLevel: 'home_only' | 'national' | 'global' | null = null;
  renewalBillingCycle: 'monthly' | 'annually' = 'monthly';
  renewalWantAddons = false;
  renewalSelectedAddonIds: string[] = [];
  renewalTierOptions: Array<{ level: 'home_only' | 'national' | 'global'; title: string; subtitle: string; min: number; max: number }> = [];
  renewalForm = { autoRenew: true };
  private readonly money = new Intl.NumberFormat('de-AT', { style: 'currency', currency: 'EUR' });
  emergencyList: EmergencyRow[] = [];
  ecLoading = false;
  ecModalOpen = false;
  ecSaving = false;
  ecError = '';

  ecForm = {
    firstName: '',
    lastName: '',
    phoneNumber: '',
    email: '',
    relation: '',
    priority: 1,
  };

  myBookings: any[] = [];
  upcomingSessions: any[] = [];
  bookingsLoading = false;
  sessionsLoading = false;
  sessionsBusy = false;
  sessionsError = '';
  sessionsMsg = '';

  constructor(
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit() {
    this.loadProfile();
    this.loadEmergency();
    this.loadSubscription();
    this.loadMyBookings();
    this.loadUpcomingSessions();
  }

  formatMoney(n: number) {
    return this.money.format(n);
  }

  formatDate(iso: string | null | undefined) {
    if (!iso) return '—';
    const [y, m, d] = iso.split('-');
    return `${d}.${m}.${y}`;
  }

  formatDateTime(iso: string | null | undefined) {
    if (!iso) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleString('de-AT', { dateStyle: 'short', timeStyle: 'short' });
  }

  formatSessionWhen(iso: string) {
    const d = new Date(iso);
    return d.toLocaleString('de-AT', { dateStyle: 'short', timeStyle: 'short' });
  }

  loadMyBookings() {
    this.bookingsLoading = true;
    this.http
      .get<any[]>('/api/MemberPortal/sessions/my-bookings', { headers: this.headers() })
      .pipe(finalize(() => (this.bookingsLoading = false)))
      .subscribe({
        next: (rows) => {
          this.myBookings = rows;
          this.cdr.detectChanges();
        },
        error: () => {
          this.myBookings = [];
        },
      });
  }

  loadUpcomingSessions() {
    this.sessionsLoading = true;
    this.sessionsError = '';
    this.http
      .get<any[]>('/api/MemberPortal/sessions/upcoming', { headers: this.headers() })
      .pipe(finalize(() => (this.sessionsLoading = false)))
      .subscribe({
        next: (rows) => {
          this.upcomingSessions = rows;
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.upcomingSessions = [];
          this.sessionsError = err?.error?.message ?? 'Termine konnten nicht geladen werden.';
        },
      });
  }

  bookSession(sessionId: string) {
    this.sessionsBusy = true;
    this.sessionsError = '';
    this.sessionsMsg = '';
    this.http.post(`/api/MemberPortal/sessions/${sessionId}/book`, {}, { headers: this.headers() }).subscribe({
      next: (r: any) => {
        this.sessionsBusy = false;
        this.sessionsError = '';
        this.sessionsMsg = r?.message ?? 'Angemeldet.';
        this.loadMyBookings();
        this.loadUpcomingSessions();
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.sessionsBusy = false;
        this.sessionsError = err?.error?.message ?? 'Anmeldung fehlgeschlagen.';
        this.cdr.detectChanges();
      },
    });
  }

  cancelBooking(bookingId: string) {
    if (!confirm('Wirklich abmelden?')) return;
    this.sessionsBusy = true;
    this.sessionsError = '';
    this.sessionsMsg = '';
    this.http.delete(`/api/MemberPortal/sessions/bookings/${bookingId}`, { headers: this.headers() }).subscribe({
      next: (r: any) => {
        this.sessionsBusy = false;
        this.sessionsError = '';
        this.sessionsMsg = r?.message ?? 'Abgemeldet.';
        this.loadMyBookings();
        this.loadUpcomingSessions();
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.sessionsBusy = false;
        this.sessionsError = err?.error?.message ?? 'Abmeldung fehlgeschlagen.';
        this.cdr.detectChanges();
      },
    });
  }

  private headers() {
    return new HttpHeaders().set('Authorization', 'Bearer ' + localStorage.getItem('token'));
  }

  loadProfile() {
    this.profileLoading = true;
    this.profileError = '';
    this.http
      .get<MemberProfile>('/api/MemberPortal/profile', { headers: this.headers() })
      .pipe(
        timeout(15_000),
        finalize(() => {
          this.profileLoading = false;
          this.finishFirstLoad();
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: (p) => {
          this.profile = p;
        },
        error: (err: unknown) => {
          this.profile = null;

          if (err instanceof TimeoutError) {
            this.profileError = 'Timeout beim Laden des Profils (15s). Läuft die API und ist der Proxy aktiv?';
            return;
          }

          if (err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)) {
            // Token invalid or wrong role (e.g. staff token on member route)
            localStorage.removeItem('token');
            this.profileError = 'Session abgelaufen oder falscher Account. Bitte erneut einloggen.';
            return;
          }

          const anyErr = err as any;
          const msg = anyErr?.error?.message ?? anyErr?.error?.Message;
          this.profileError =
            (typeof msg === 'string' && msg) ||
            `Profil konnte nicht geladen werden. (${anyErr?.status ?? 'unknown'})`;
        },
      });
  }

  loadSubscription() {
    this.subLoading = true;
    this.http
      .get<SubscriptionDto>('/api/MemberPortal/subscription', { headers: this.headers() })
      .pipe(
        timeout(15_000),
        finalize(() => {
          this.subLoading = false;
          this.finishFirstLoad();
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: (s) => {
          this.subscription = s;
        },
        error: () => {
          this.subscription = { hasContract: false };
        },
      });
  }

  private reloadSub(msg?: string) {
    this.subActionMsg = msg ?? '';
    this.subActionError = '';
    this.loadSubscription();
  }

  pauseSub() {
    if (!confirm('Pause zum Monatsende planen? Ab dem 1. des Folgemonats ist das Abo pausiert.')) return;
    this.subBusy = true;
    this.http.post<{ message?: string }>('/api/MemberPortal/subscription/pause', {}, { headers: this.headers() }).subscribe({
      next: (r) => {
        this.subBusy = false;
        this.reloadSub(r.message);
      },
      error: (err) => {
        this.subBusy = false;
        this.subActionError = err.error?.message ?? 'Pause fehlgeschlagen.';
      },
    });
  }

  resumeSub() {
    const undo = this.subscription?.canUndoCancel;
    const msg = undo
      ? 'Kündigung zurücknehmen? Der Vertrag verlängert sich danach wieder automatisch.'
      : this.subscription?.phase === 'pause_scheduled'
        ? 'Geplante Pause wirklich abbrechen?'
        : 'Abo ab dem 1. des Folgemonats reaktivieren?';
    if (!confirm(msg)) return;
    this.subBusy = true;
    this.http.post<{ message?: string }>('/api/MemberPortal/subscription/resume', {}, { headers: this.headers() }).subscribe({
      next: (r) => {
        this.subBusy = false;
        this.reloadSub(r.message);
      },
      error: (err) => {
        this.subBusy = false;
        this.subActionError = err.error?.message ?? 'Aktion fehlgeschlagen.';
      },
    });
  }

  cancelSub() {
    if (!confirm('Vertrag zum Laufzeitende kündigen? Bis dahin bleibt das Abo aktiv (Pause/Reaktivierung möglich).')) return;
    this.subBusy = true;
    this.http.post<{ message?: string }>('/api/MemberPortal/subscription/cancel', {}, { headers: this.headers() }).subscribe({
      next: (r) => {
        this.subBusy = false;
        this.reloadSub(r.message);
      },
      error: (err) => {
        this.subBusy = false;
        this.subActionError = err.error?.message ?? 'Kündigung fehlgeschlagen.';
      },
    });
  }

  openRenewalModal() {
    this.renewalError = '';
    this.renewalModalOpen = true;
    this.renewalStep = 'tier';
    this.renewalLoading = true;
    this.http.get<RenewalPlanCatalog>('/api/MemberPortal/subscription/renewal-plan', { headers: this.headers() }).subscribe({
      next: (cat) => {
        this.renewalCatalog = cat;
        this.renewalAccessLevel = (cat.current.accessLevel as 'home_only' | 'national' | 'global') || 'home_only';
        this.renewalBillingCycle = cat.current.billingCycle === 'annually' ? 'annually' : 'monthly';
        this.renewalWantAddons = cat.current.addonPriceIds.length > 0;
        this.renewalSelectedAddonIds = this.addonIdsFromPriceIds(cat.current.addonPriceIds);
        this.rebuildRenewalTierOptions();
        this.renewalLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.renewalCatalog = null;
        this.renewalLoading = false;
        this.renewalError = 'Optionen konnten nicht geladen werden.';
      },
    });
  }

  closeRenewalModal() {
    this.renewalModalOpen = false;
  }

  renewalGoAddons() {
    if (!this.renewalAccessLevel) {
      this.renewalError = 'Bitte ein Abo-Modell wählen.';
      return;
    }
    this.renewalError = '';
    this.renewalStep = 'addons';
  }

  private addonIdsFromPriceIds(priceIds: string[]) {
    if (!this.renewalCatalog) return [];
    const ids = new Set<string>();
    for (const pid of priceIds) {
      const row = this.renewalCatalog.addonPrices.find((x) => x.addonPriceId === pid);
      if (row) ids.add(row.addonId);
    }
    return Array.from(ids);
  }

  private resolveRenewalTierPriceId() {
    const row = this.renewalCatalog?.tierPrices.find(
      (x) => x.accessLevel === this.renewalAccessLevel && x.billingCycle === this.renewalBillingCycle,
    );
    return row?.tierPriceId ?? '';
  }

  private resolveRenewalAddonPriceIds() {
    if (!this.renewalCatalog || !this.renewalWantAddons) return [] as string[];
    const out: string[] = [];
    for (const aid of this.renewalSelectedAddonIds) {
      const row = this.renewalCatalog.addonPrices.find(
        (x) => x.addonId === aid && x.billingCycle === this.renewalBillingCycle,
      );
      if (row) out.push(row.addonPriceId);
    }
    return out;
  }

  toRenewalMonthly(amount: number, cycle: string) {
    return cycle === 'annually' ? Math.round((amount / 12) * 100) / 100 : amount;
  }

  rebuildRenewalTierOptions() {
    if (!this.renewalCatalog) {
      this.renewalTierOptions = [];
      return;
    }
    const levels: Array<'home_only' | 'national' | 'global'> = ['home_only', 'national', 'global'];
    this.renewalTierOptions = levels.map((level) => {
      const m = this.renewalCatalog!.tierPrices.find((x) => x.accessLevel === level && x.billingCycle === 'monthly');
      const y = this.renewalCatalog!.tierPrices.find((x) => x.accessLevel === level && x.billingCycle === 'annually');
      const min =
        m && y
          ? Math.min(this.toRenewalMonthly(m.amount, m.billingCycle), this.toRenewalMonthly(y.amount, y.billingCycle))
          : 0;
      const max =
        m && y
          ? Math.max(this.toRenewalMonthly(m.amount, m.billingCycle), this.toRenewalMonthly(y.amount, y.billingCycle))
          : 0;
      return { level, title: this.renewalTierTitle(level), subtitle: this.renewalTierSubtitle(level), min, max };
    });
  }

  renewalTierTitle(level: string | null) {
    if (level === 'home_only') return 'Local';
    if (level === 'national') return 'National';
    if (level === 'global') return 'Global';
    return '—';
  }

  renewalTierSubtitle(level: string) {
    if (level === 'home_only') return 'Nur Heimat-Standort';
    if (level === 'national') return 'Alle Standorte im Land';
    return 'Alle Standorte (AT + DE)';
  }

  renewalUniquePackages(): RenewalAddonPackageView[] {
    if (!this.renewalCatalog) return [];
    const byId = new Map<string, RenewalAddonPackageView>();
    for (const row of this.renewalCatalog.addonPrices) {
      let cur = byId.get(row.addonId);
      if (!cur) {
        const f = {
          sauna: !!row.includesSauna,
          solarium: !!row.includesSolarium,
          drinks: !!row.includesDrinks,
          coffee: !!row.includesCoffee,
        };
        cur = {
          addonId: row.addonId,
          addonName: row.addonName,
          isCombo: row.isCombo,
          isAllIn: f.sauna && f.solarium && f.drinks && f.coffee,
          includesSauna: f.sauna,
          includesSolarium: f.solarium,
          includesDrinks: f.drinks,
          includesCoffee: f.coffee,
          priceMonthly: 0,
          priceAnnual: 0,
        };
        byId.set(row.addonId, cur);
      }
      if (row.billingCycle === 'monthly') cur.priceMonthly = row.amount;
      if (row.billingCycle === 'annually') cur.priceAnnual = row.amount;
    }
    return Array.from(byId.values());
  }

  private renewalFlags(p: RenewalAddonPackageView | string): AddonFeatureFlags {
    const pkg = typeof p === 'string' ? this.renewalUniquePackages().find((x) => x.addonId === p) : p;
    return {
      sauna: !!pkg?.includesSauna,
      solarium: !!pkg?.includesSolarium,
      drinks: !!pkg?.includesDrinks,
      coffee: !!pkg?.includesCoffee,
    };
  }

  private renewalOverlap(a: AddonFeatureFlags, b: AddonFeatureFlags) {
    return (a.sauna && b.sauna) || (a.solarium && b.solarium) || (a.drinks && b.drinks) || (a.coffee && b.coffee);
  }

  renewalAddonDisabled(p: RenewalAddonPackageView) {
    if (this.renewalSelectedAddonIds.includes(p.addonId)) return false;
    const pf = this.renewalFlags(p);
    return this.renewalSelectedAddonIds.some((id) => this.renewalOverlap(pf, this.renewalFlags(id)));
  }

  renewalToggleAddon(p: RenewalAddonPackageView, checked: boolean) {
    let next = new Set(this.renewalSelectedAddonIds);
    if (checked) {
      for (const id of [...next]) {
        if (this.renewalOverlap(this.renewalFlags(id), this.renewalFlags(p))) next.delete(id);
      }
      next.add(p.addonId);
    } else {
      next.delete(p.addonId);
    }
    this.renewalSelectedAddonIds = Array.from(next);
  }

  onRenewalWantAddonsChange(v: boolean) {
    if (!v) this.renewalSelectedAddonIds = [];
  }

  renewalAddonFlex(p: RenewalAddonPackageView) {
    return this.toRenewalMonthly(p.priceMonthly, 'monthly');
  }

  renewalAddonCommit(p: RenewalAddonPackageView) {
    return this.toRenewalMonthly(p.priceAnnual, 'annually');
  }

  renewalPreviewTotal() {
    if (!this.renewalCatalog || !this.renewalAccessLevel) return 0;
    const tier = this.renewalCatalog.tierPrices.find(
      (x) => x.accessLevel === this.renewalAccessLevel && x.billingCycle === this.renewalBillingCycle,
    );
    let sum = tier ? this.toRenewalMonthly(tier.amount, tier.billingCycle) : 0;
    if (this.renewalWantAddons) {
      for (const aid of this.renewalSelectedAddonIds) {
        const row = this.renewalCatalog.addonPrices.find(
          (x) => x.addonId === aid && x.billingCycle === this.renewalBillingCycle,
        );
        if (row) sum += this.toRenewalMonthly(row.amount, row.billingCycle);
      }
    }
    if (this.renewalCatalog.isVerifiedStudent) sum = Math.round(sum * 90) / 100;
    return sum;
  }

  saveRenewal() {
    const tierPriceId = this.resolveRenewalTierPriceId();
    if (!tierPriceId) {
      this.renewalError = 'Bitte ein gültiges Abo wählen.';
      return;
    }
    this.renewalSaving = true;
    this.renewalError = '';
    this.http
      .put(
        '/api/MemberPortal/subscription/renewal',
        {
          renewalTierPriceId: tierPriceId,
          autoRenew: this.renewalForm.autoRenew,
          renewalAddonPriceIds: this.resolveRenewalAddonPriceIds(),
        },
        { headers: this.headers() },
      )
      .subscribe({
        next: () => {
          this.renewalSaving = false;
          this.closeRenewalModal();
          this.reloadSub('Folge-Abo gespeichert.');
        },
        error: (err) => {
          this.renewalSaving = false;
          this.renewalError = err.error?.message ?? 'Speichern fehlgeschlagen.';
        },
      });
  }

  loadEmergency() {
    this.ecLoading = true;
    this.http
      .get<EmergencyRow[]>('/api/MemberPortal/emergency-contacts', { headers: this.headers() })
      .pipe(
        timeout(15_000),
        finalize(() => {
          this.ecLoading = false;
          this.finishFirstLoad();
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: (rows) => {
          this.emergencyList = rows;
        },
        error: () => {
          this.emergencyList = [];
        },
      });
  }

  private finishFirstLoad() {
    if (this.pendingFirstLoads <= 0) return;
    this.pendingFirstLoads -= 1;
    if (this.pendingFirstLoads === 0) this.pageLoading = false;
  }

  openEcModal() {
    this.ecError = '';
    this.ecForm = { firstName: '', lastName: '', phoneNumber: '', email: '', relation: '', priority: 1 };
    this.ecModalOpen = true;
  }

  closeEcModal() {
    this.ecModalOpen = false;
  }

  submitEmergency() {
    this.ecSaving = true;
    this.ecError = '';
    const body = {
      firstName: this.ecForm.firstName.trim(),
      lastName: this.ecForm.lastName.trim() || null,
      phoneNumber: this.ecForm.phoneNumber.trim(),
      email: this.ecForm.email.trim() || null,
      relation: this.ecForm.relation.trim() || null,
      priority: Number(this.ecForm.priority) || 1,
    };
    this.http.post('/api/MemberPortal/emergency-contacts', body, { headers: this.headers() }).subscribe({
      next: () => {
        this.ecSaving = false;
        this.closeEcModal();
        this.loadEmergency();
      },
      error: (err) => {
        this.ecSaving = false;
        this.ecError = err.error?.message ?? 'Speichern fehlgeschlagen.';
      },
    });
  }

  logout() {
    localStorage.removeItem('token');
    window.location.href = '/login';
  }
}
