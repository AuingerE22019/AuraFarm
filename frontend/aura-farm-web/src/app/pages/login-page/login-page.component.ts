import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router, ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';

interface MemberSetupPayload {
  oldUsername: string;
  oldPassword: string;
  newUsername: string;
  newPassword: string;
  email: string;
  paymentMethod: string;
  homeLocationId: string;
  tierPriceId: string;
  addonPriceIds: string[];
  dateOfBirth: string;
  phone: string;
  street: string;
  houseNumber: string;
  zipCode: string;
  city: string;
  countryIso: string;
}

interface TierPriceRow {
  tierPriceId: string;
  billingCycle: string;
  amount: number;
  currency: string;
  tierId: string;
  tierName: string;
  accessLevel: string;
}

interface AddonPriceRow {
  addonPriceId: string;
  billingCycle: string;
  amount: number;
  currency: string;
  addonId: string;
  addonName: string;
  isCombo: boolean;
  includesSauna?: boolean;
  includesSolarium?: boolean;
  includesDrinks?: boolean;
  includesCoffee?: boolean;
}

interface AddonFeatureFlags {
  sauna: boolean;
  solarium: boolean;
  drinks: boolean;
  coffee: boolean;
}

interface AddonPackageView {
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

interface SetupOptionsResponse {
  defaultLocationId: string | null;
  isVerifiedStudent?: boolean;
  locationsByCountry: Record<string, Array<{ locationId: string; name: string; countryIso: string; city: string }>>;
  tierPrices: TierPriceRow[];
  addonPrices: AddonPriceRow[];
}

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-container">
      <div class="login-card" *ngIf="!isSetupMode">
        <h2>Member Login</h2>
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

          <button type="submit" class="btn-submit" [disabled]="loading">Login</button>
        </form>

        <p class="footer-link">
          <a href="javascript:void(0)" (click)="startSetupFromLogin()">Erster Login? Account mit PDF-Daten einrichten</a>
        </p>
      </div>

      <div class="login-card setup-card" *ngIf="isSetupMode">
        <h2>Account einrichten</h2>
        <p class="intro">
          Schritt für Schritt: Zugangsdaten, Profil, Standort, Abo-Modell, optionale Pakete, Zahlungsrhythmus und eine
          Bestellübersicht. Notfallkontakte trägst du nach dem Login unter <strong>/dashboard</strong> ein.
        </p>
        <div class="error" *ngIf="error">{{ error }}</div>

        <form *ngIf="setupStep === 'temp'" (ngSubmit)="submitTempCredentials()">
          <h3 class="section-title">Schritt 1: Temporärer Zugang (aus PDF)</h3>
          <div class="form-group">
            <label>Temp. Username</label>
            <input type="text" [(ngModel)]="setupData.oldUsername" name="oldUser" required />
          </div>
          <div class="form-group">
            <label>Temp. Passwort</label>
            <div class="password-input-wrap">
              <input [type]="showOldPassword ? 'text' : 'password'" [(ngModel)]="setupData.oldPassword" name="oldPass" required />
              <button type="button" class="toggle-pwd" (click)="showOldPassword = !showOldPassword">
                {{ showOldPassword ? '🙈' : '👁️' }}
              </button>
            </div>
          </div>

          <button type="submit" class="btn-submit" [disabled]="loading">Weiter</button>
        </form>

        <form *ngIf="setupStep === 'details'" (ngSubmit)="goToLocationStep()">
          <h3 class="section-title">Schritt 2: Eigene Daten & dauerhafter Login</h3>

          <div class="form-row">
            <div class="form-group">
              <label>Neuer Username</label>
              <input type="text" [(ngModel)]="setupData.newUsername" name="newUser" required />
            </div>
            <div class="form-group">
              <label>Neues Passwort</label>
              <div class="password-input-wrap">
                <input [type]="showNewPassword ? 'text' : 'password'" [(ngModel)]="setupData.newPassword" name="newPass" required />
                <button type="button" class="toggle-pwd" (click)="showNewPassword = !showNewPassword">
                  {{ showNewPassword ? '🙈' : '👁️' }}
                </button>
              </div>
            </div>
          </div>
          <div class="form-group">
            <label>E-Mail</label>
            <input type="email" [(ngModel)]="setupData.email" name="newEmail" required />
          </div>

          <hr class="sep" />

          <h3 class="section-title">Persönliche Daten</h3>
          <div class="form-row">
            <div class="form-group">
              <label>Geburtsdatum</label>
              <input type="date" [(ngModel)]="setupData.dateOfBirth" name="dob" required />
            </div>
            <div class="form-group">
              <label>Telefon</label>
              <input type="text" [(ngModel)]="setupData.phone" name="phone" required />
            </div>
          </div>

          <h3 class="section-title">Adresse</h3>
          <div class="form-group">
            <label>Straße</label>
            <input type="text" [(ngModel)]="setupData.street" name="street" required />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Hausnummer</label>
              <input type="text" [(ngModel)]="setupData.houseNumber" name="hn" />
            </div>
            <div class="form-group">
              <label>PLZ</label>
              <input type="text" [(ngModel)]="setupData.zipCode" name="zip" required />
            </div>
            <div class="form-group">
              <label>Ort</label>
              <input type="text" [(ngModel)]="setupData.city" name="city" required />
            </div>
            <div class="form-group">
              <label>Land (ISO, z. B. AT)</label>
              <input type="text" [(ngModel)]="setupData.countryIso" name="country" maxlength="2" required />
            </div>
          </div>

          <div class="form-group">
            <label>Zahlungsmethode</label>
            <select [(ngModel)]="setupData.paymentMethod" name="payment" required>
              <option value="card">Kreditkarte</option>
              <option value="transfer">Überweisung</option>
              <option value="direct_debit">SEPA Lastschrift</option>
            </select>
          </div>

          <button type="button" class="btn-submit" (click)="goBackToTempStep()" [disabled]="loading">Zurück</button>
          <button type="submit" class="btn-submit" [disabled]="loading">Weiter</button>
        </form>

        <form *ngIf="setupStep === 'location'" (ngSubmit)="goToTierStep()">
          <h3 class="section-title">Schritt 3: Standort</h3>
          <p class="intro" *ngIf="setupOptions?.defaultLocationId">Vorausgewählt: Recruiter-Standort (du kannst wechseln).</p>

          <div *ngIf="!setupOptions" class="error">Setup-Optionen fehlen. Bitte zurück und erneut versuchen.</div>

          <div *ngIf="setupOptions" class="locations">
            <div class="country" *ngFor="let iso of setupOptions.locationsByCountry | keyvalue">
              <h4 class="section-title">Land: {{ iso.key }}</h4>
              <div class="loc-list">
                <label class="loc" *ngFor="let l of iso.value">
                  <input type="radio" name="homeLoc" [value]="l.locationId" [(ngModel)]="setupData.homeLocationId" required />
                  <span>{{ l.city }} — {{ l.name }}</span>
                </label>
              </div>
            </div>
          </div>

          <button type="button" class="btn-submit" (click)="setupStep = 'details'" [disabled]="loading">Zurück</button>
          <button type="submit" class="btn-submit" [disabled]="loading">Weiter</button>
        </form>

        <div *ngIf="setupStep === 'tier'" class="wizard-block">
          <h3 class="section-title">Schritt 4: Abo-Modell</h3>
          <p class="intro">Alle Preise sind Monatsbeiträge. Günstigerer Tarif bei 12 Monaten Mindestlaufzeit (weiterhin monatliche Zahlung).</p>

          <div class="tier-grid" *ngIf="setupOptions">
            <button
              type="button"
              class="tier-card"
              *ngFor="let opt of tierOptions"
              [class.selected]="selectedAccessLevel === opt.level"
              (click)="selectedAccessLevel = opt.level"
            >
              <div class="tier-title">{{ opt.title }}</div>
              <div class="tier-sub">{{ opt.subtitle }}</div>
              <div class="tier-range">ca. {{ formatMoney(opt.min) }} – {{ formatMoney(opt.max) }} / Monat</div>
            </button>
          </div>

          <button type="button" class="btn-submit" (click)="setupStep = 'location'" [disabled]="loading">Zurück</button>
          <button type="button" class="btn-submit" (click)="goToAddonStep()" [disabled]="loading || !selectedAccessLevel">Weiter</button>
        </div>

        <div *ngIf="setupStep === 'addons'" class="wizard-block">
          <h3 class="section-title">Schritt 5: Zusatzpakete</h3>
          <p class="intro">Alle Preise als Monatsbeitrag. Im nächsten Schritt wählst du flexible Laufzeit oder 12 Monate Bindung.</p>

          <div class="form-group">
            <label>Pakete?</label>
            <select [(ngModel)]="wantAddonPackages" name="wantAddons" (ngModelChange)="onWantAddonsChange($event)">
              <option [ngValue]="false">Nein, nur Basis-Abo</option>
              <option [ngValue]="true">Ja, Zusatzpakete auswählen</option>
            </select>
          </div>

          <div *ngIf="wantAddonPackages && setupOptions" class="addon-list">
            <p class="muted small">Bundles schließen sich mit Paketen mit gleichen Leistungen aus (z. B. All In = alles).</p>
            <label
              class="addon"
              *ngFor="let p of uniqueAddonPackages()"
              [class.addon-disabled]="isAddonDisabled(p)"
            >
              <input
                type="checkbox"
                [checked]="selectedAddonIds.includes(p.addonId)"
                [disabled]="isAddonDisabled(p)"
                (change)="toggleAddon(p, $any($event.target).checked)"
              />
              <span>
                <strong>{{ p.addonName }}</strong>
                <span *ngIf="p.isAllIn" class="tag tag-allin">All In</span>
                <span *ngIf="p.isCombo && !p.isAllIn" class="tag">Bundle</span>
                <div class="muted small">{{ addonFeatureLabel(p) }}</div>
                <div class="muted small">
                  {{ formatMoney(addonMonthlyFlex(p)) }} / Monat (flexibel) ·
                  {{ formatMoney(addonMonthlyCommitment(p)) }} / Monat (12 Mon. Laufzeit)
                </div>
                <div class="muted small" *ngIf="isAddonDisabled(p)">Bereits durch andere Auswahl abgedeckt.</div>
              </span>
            </label>
          </div>

          <button type="button" class="btn-submit" (click)="setupStep = 'tier'" [disabled]="loading">Zurück</button>
          <button type="button" class="btn-submit" (click)="goToBillingStep()" [disabled]="loading">Weiter</button>
        </div>

        <div *ngIf="setupStep === 'billing'" class="wizard-block">
          <h3 class="section-title">Schritt 6: Laufzeit</h3>
          <p class="intro">Du zahlst immer monatlich. Bei 12-Monats-Laufzeit bist du an ein Jahr gebunden, der Monatsbeitrag ist oft günstiger.</p>

          <div class="form-group">
            <label>Vertragslaufzeit</label>
            <select [(ngModel)]="selectedBillingCycle" name="cycle" (ngModelChange)="onBillingCycleChange($event)">
              <option value="monthly">Flexibel (monatlich kündbar)</option>
              <option value="annually">12 Monate Mindestlaufzeit</option>
            </select>
          </div>

          <div class="preview-box" *ngIf="setupOptions">
            <div><strong>Abo:</strong> {{ tierTitle(selectedAccessLevel) }}</div>
            <div><strong>Monatsbeitrag Abo:</strong> {{ formatMoney(currentTierAmount()) }} / Monat</div>
            <div *ngIf="wantAddonPackages && selectedAddonIds.length">
              <strong>Monatsbeitrag Pakete:</strong> {{ formatMoney(currentAddonSum()) }} / Monat
            </div>
            <div><strong>Zwischensumme:</strong> {{ formatMoney(previewGross()) }} / Monat</div>
            <div class="muted small" *ngIf="selectedBillingCycle === 'annually'">Mindestlaufzeit 12 Monate, Abbuchung jeden Monat.</div>
          </div>

          <button type="button" class="btn-submit" (click)="setupStep = 'addons'" [disabled]="loading">Zurück</button>
          <button type="button" class="btn-submit" (click)="goToSummaryStep()" [disabled]="loading">Weiter zur Übersicht</button>
        </div>

        <div *ngIf="setupStep === 'summary'" class="wizard-block">
          <h3 class="section-title">Schritt 7: Bestellübersicht</h3>

          <div class="summary-card" *ngIf="setupOptions">
            <h4>Auswahl</h4>
            <ul class="sum-list">
              <li><span>Standort</span><span>{{ locationLabel() }}</span></li>
              <li><span>Abo-Modell</span><span>{{ tierTitle(selectedAccessLevel) }}</span></li>
              <li><span>Vertragslaufzeit</span><span>{{ billingTermLabel() }}</span></li>
              <li *ngIf="!wantAddonPackages"><span>Pakete</span><span>— keine —</span></li>
              <li *ngIf="wantAddonPackages && selectedAddonIds.length === 0"><span>Pakete</span><span>— keine ausgewählt —</span></li>
              <li *ngFor="let n of selectedAddonNames()"><span>Paket</span><span>{{ n }}</span></li>
            </ul>

            <h4>Preise (monatliche Zahlung)</h4>
            <ul class="sum-list">
              <li><span>Abo / Monat</span><span>{{ formatMoney(currentTierAmount()) }}</span></li>
              <li *ngIf="wantAddonPackages && selectedAddonIds.length">
                <span>Pakete / Monat</span><span>{{ formatMoney(currentAddonSum()) }}</span>
              </li>
              <li><span>Regulärer Monatsbeitrag</span><span>{{ formatMoney(previewTotal()) }}</span></li>
              <li class="total"><span>Erste Zahlung (heute, anteilig)</span><span>{{ formatMoney(proratedFirstPayment()) }}</span></li>
              <li *ngIf="setupOptions.isVerifiedStudent" class="save">
                <span>Studentenrabatt (10%)</span><span>− {{ formatMoney(studentSavings()) }}</span>
              </li>
              <li *ngFor="let line of bundleSavingsLines()" class="save">
                <span>{{ line.label }}</span><span>− {{ formatMoney(line.amount) }}</span>
              </li>
            </ul>
            <p class="muted small" *ngIf="selectedBillingCycle === 'annually'">
              12 Kalendermonate (jeweils 1.–1.), monatliche Abbuchung zum vollen Monatsbeitrag.
            </p>
            <p class="muted small">
              Zahltag ist immer der <strong>1.</strong> Heute zahlst du nur die Tage bis Monatsende; ab dem 1. des Folgemonats den vollen Betrag.
            </p>
            <p class="muted small" *ngIf="setupOptions.isVerifiedStudent">
              Als verifizierter Student erhältst du 10% auf den Gesamtpreis (Mock).
            </p>
          </div>

          <button type="button" class="btn-submit" (click)="setupStep = 'billing'" [disabled]="loading">Zurück</button>
          <button type="button" class="btn-submit" (click)="finalizeSetup()" [disabled]="loading">Kostenpflichtig abschließen & einloggen</button>
        </div>

        <p class="footer-link">
          <a href="javascript:void(0)" (click)="goBackToLogin()">Zurück zum Login</a>
        </p>
      </div>
    </div>
  `,
  styles: [
    `
      .login-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        background: var(--bg-page);
        padding: 2rem;
      }
      .login-card {
        background: var(--surface-1);
        padding: 2rem;
        border-radius: var(--radius-panel);
        border: 1px solid var(--border);
        box-shadow: var(--shadow-panel);
        width: 100%;
        max-width: 420px;
        color: var(--text);
      }
      .setup-card {
        max-width: 760px;
      }
      .intro {
        color: var(--text-secondary);
        font-size: 0.9rem;
        margin-bottom: 1rem;
      }
      .section-title {
        margin: 0.5rem 0;
        font-size: 1rem;
        color: var(--text-secondary);
      }
      .form-row {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
        gap: 0.75rem;
      }
      .form-group {
        margin-bottom: 0.75rem;
      }
      .form-group label {
        display: block;
        margin-bottom: 0.35rem;
        font-weight: bold;
        color: var(--text-secondary);
        font-size: 0.85rem;
      }
      .form-group input,
      .form-group select {
        width: 100%;
        padding: 0.5rem;
        border: 1px solid var(--border-strong);
        border-radius: 4px;
        box-sizing: border-box;
        background: var(--void-0);
        color: var(--text);
      }
      .password-input-wrap {
        display: flex;
        position: relative;
      }
      .password-input-wrap input {
        padding-right: 2.5rem;
      }
      .toggle-pwd {
        position: absolute;
        right: 5px;
        top: 50%;
        transform: translateY(-50%);
        background: none;
        border: none;
        padding: 0;
        width: auto;
        color: var(--text-muted);
        cursor: pointer;
        font-size: 1.2rem;
      }
      .toggle-pwd:hover {
        color: var(--text);
      }
      .btn-submit {
        width: 100%;
        padding: 0.75rem;
        background: var(--accent-cyan);
        color: #000;
        font-weight: bold;
        border: none;
        border-radius: 4px;
        cursor: pointer;
        font-size: 1rem;
        margin-top: 1rem;
      }
      .btn-submit:disabled {
        background: var(--muted);
        cursor: not-allowed;
      }
      .error {
        color: var(--danger-text);
        background: var(--danger-bg);
        border: 1px solid var(--danger-border);
        padding: 0.5rem;
        border-radius: 4px;
        margin-bottom: 1rem;
      }
      .sep {
        margin: 1rem 0;
        border: 0;
        border-top: 1px solid var(--border);
      }
      .footer-link {
        margin-top: 1rem;
        text-align: center;
      }
      .locations {
        margin-top: 0.75rem;
      }
      .loc-list,
      .addon-list {
        display: grid;
        gap: 0.5rem;
        margin-top: 0.5rem;
      }
      .loc,
      .addon {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        padding: 0.5rem 0.6rem;
        border: 1px solid var(--border);
        border-radius: 8px;
        background: rgba(15, 23, 42, 0.35);
      }
      .tier-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
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
        font-size: 1.05rem;
      }
      .tier-sub {
        color: var(--text-secondary);
        font-size: 0.85rem;
        margin: 0.35rem 0;
      }
      .tier-range {
        font-weight: 700;
        color: var(--accent-cyan);
      }
      .wizard-block {
        margin-top: 0.25rem;
      }
      .preview-box {
        margin-top: 0.75rem;
        padding: 0.75rem 1rem;
        border: 1px solid var(--border);
        border-radius: 10px;
        background: rgba(15, 23, 42, 0.35);
        display: grid;
        gap: 0.35rem;
      }
      .summary-card {
        border: 1px solid var(--border);
        border-radius: 12px;
        padding: 1rem;
        background: rgba(15, 23, 42, 0.35);
      }
      .sum-list {
        list-style: none;
        padding: 0;
        margin: 0.5rem 0 1rem;
      }
      .sum-list li {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        padding: 0.35rem 0;
        border-bottom: 1px solid rgba(148, 163, 184, 0.12);
      }
      .sum-list li.save {
        color: var(--accent-teal);
      }
      .sum-list li.total {
        font-weight: 900;
        font-size: 1.05rem;
        border-bottom: 0;
        margin-top: 0.25rem;
      }
      .muted {
        color: var(--text-secondary);
      }
      .small {
        font-size: 0.82rem;
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
      .addon-disabled {
        opacity: 0.55;
      }
      a {
        color: var(--accent-cyan);
        text-decoration: none;
      }
      a:hover {
        text-decoration: underline;
      }
    `,
  ],
})
export class LoginPageComponent implements OnInit {
  isSetupMode = false;
  setupStep: 'temp' | 'details' | 'tier' | 'addons' | 'billing' | 'location' | 'summary' = 'temp';
  email = '';
  password = '';
  showPassword = false;
  showOldPassword = false;
  showNewPassword = false;
  loading = false;
  error = '';

  setupData: MemberSetupPayload = {
    oldUsername: '',
    oldPassword: '',
    newUsername: '',
    newPassword: '',
    email: '',
    paymentMethod: 'card',
    homeLocationId: '',
    tierPriceId: '',
    addonPriceIds: [],
    dateOfBirth: '',
    phone: '',
    street: '',
    houseNumber: '',
    zipCode: '',
    city: '',
    countryIso: 'AT',
  };

  setupOptions: SetupOptionsResponse | null = null;
  selectedBillingCycle: 'monthly' | 'annually' = 'monthly';
  selectedAccessLevel: 'home_only' | 'national' | 'global' | null = null;
  wantAddonPackages = false;
  selectedAddonIds: string[] = [];

  tierOptions: Array<{
    level: 'home_only' | 'national' | 'global';
    title: string;
    subtitle: string;
    min: number;
    max: number;
  }> = [];

  private readonly money = new Intl.NumberFormat('de-AT', { style: 'currency', currency: 'EUR' });

  constructor(
    private http: HttpClient,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe((params) => {
      if (params['setup'] === 'true') {
        this.isSetupMode = true;
        this.setupData.oldUsername = params['u'] || '';
        this.setupData.oldPassword = params['p'] || '';
        if (this.setupData.oldUsername && this.setupData.oldPassword) {
          this.setupStep = 'details';
          void this.fetchSetupOptions();
        } else {
          this.setupStep = 'temp';
        }
      }
    });
  }

  formatMoney(n: number) {
    return this.money.format(n);
  }

  /** DB stores annual rows as total per year; UI and contract use monthly payment. */
  toMonthlyRate(amount: number, billingCycle: string) {
    return billingCycle === 'annually' ? Math.round((amount / 12) * 100) / 100 : amount;
  }

  billingTermLabel() {
    return this.selectedBillingCycle === 'monthly'
      ? 'Flexibel (monatlich kündbar)'
      : '12 Monate Mindestlaufzeit';
  }

  addonMonthlyFlex(p: AddonPackageView) {
    return this.toMonthlyRate(p.priceMonthly, 'monthly');
  }

  addonMonthlyCommitment(p: AddonPackageView) {
    return this.toMonthlyRate(p.priceAnnual, 'annually');
  }

  startSetupFromLogin() {
    this.error = '';
    this.isSetupMode = true;
    this.setupStep = 'temp';
  }

  goBackToLogin() {
    this.error = '';
    this.isSetupMode = false;
    this.setupStep = 'temp';
  }

  submitTempCredentials() {
    this.error = '';
    if (!this.setupData.oldUsername?.trim() || !this.setupData.oldPassword) {
      this.error = 'Bitte temp. Username und temp. Passwort aus dem PDF eingeben.';
      return;
    }
    this.setupStep = 'details';
    void this.fetchSetupOptions();
  }

  goBackToTempStep() {
    this.error = '';
    this.setupStep = 'temp';
  }

  async fetchSetupOptions() {
    this.loading = true;
    this.error = '';
    try {
      const res = await firstValueFrom(
        this.http.post<SetupOptionsResponse>('/api/Registration/member/setup/options', {
          oldUsername: this.setupData.oldUsername,
          oldPassword: this.setupData.oldPassword,
        }),
      );
      this.setupOptions = res;

      const firstLoc = Object.values(res.locationsByCountry).flat()[0]?.locationId ?? '';
      this.setupData.homeLocationId = res.defaultLocationId ?? firstLoc;

      this.selectedAccessLevel = 'home_only';
      this.wantAddonPackages = false;
      this.selectedAddonIds = [];
      this.selectedBillingCycle = 'monthly';
      this.rebuildTierOptions();
      this.syncPayloadFromWizard();
    } catch (e: any) {
      this.setupOptions = null;
      this.error = e?.error?.message ?? 'Konnte Setup-Optionen nicht laden.';
    } finally {
      this.loading = false;
    }
  }

  private rebuildTierOptions() {
    if (!this.setupOptions) {
      this.tierOptions = [];
      return;
    }
    const levels: Array<'home_only' | 'national' | 'global'> = ['home_only', 'national', 'global'];
    this.tierOptions = levels.map((level) => {
      const r = this.tierMonthlyRange(level);
      return {
        level,
        title: this.tierTitle(level),
        subtitle: this.tierSubtitle(level),
        min: r.min,
        max: r.max,
      };
    });
  }

  tierTitle(level: string | null) {
    if (level === 'home_only') return 'Local — nur dein Heimat-Standort';
    if (level === 'national') return 'National — alle Standorte im Land';
    if (level === 'global') return 'Global — alle Standorte (AT + DE)';
    return '—';
  }

  tierSubtitle(level: string) {
    if (level === 'home_only') return 'Trainieren nur an der Location, die du auswählst.';
    if (level === 'national') return 'Alle Locations in deinem Land.';
    if (level === 'global') return 'Alle Locations in allen Ländern.';
    return '';
  }

  tierMonthlyRange(level: 'home_only' | 'national' | 'global') {
    const mRow = this.setupOptions?.tierPrices.find((x) => x.accessLevel === level && x.billingCycle === 'monthly');
    const yRow = this.setupOptions?.tierPrices.find((x) => x.accessLevel === level && x.billingCycle === 'annually');
    if (mRow == null || yRow == null) return { min: 0, max: 0 };
    const m = this.toMonthlyRate(mRow.amount, mRow.billingCycle);
    const y = this.toMonthlyRate(yRow.amount, yRow.billingCycle);
    return { min: Math.min(m, y), max: Math.max(m, y) };
  }

  goToLocationStep() {
    this.error = '';
    if (!this.setupOptions) {
      this.error = 'Bitte warte, bis die Optionen geladen sind.';
      return;
    }
    this.setupStep = 'location';
  }

  goToTierStep() {
    this.error = '';
    if (!this.setupOptions) {
      this.error = 'Bitte warte, bis die Optionen geladen sind.';
      return;
    }
    if (!this.setupData.homeLocationId) {
      this.error = 'Bitte eine Location auswählen.';
      return;
    }
    this.setupStep = 'tier';
  }

  goToAddonStep() {
    this.error = '';
    if (!this.selectedAccessLevel) {
      this.error = 'Bitte ein Abo-Modell wählen.';
      return;
    }
    this.setupStep = 'addons';
  }

  goToBillingStep() {
    this.error = '';
    this.setupStep = 'billing';
    this.onBillingCycleChange(this.selectedBillingCycle);
  }

  goToSummaryStep() {
    this.error = '';
    this.syncPayloadFromWizard();
    const err = this.validateSetupPayload();
    if (err) {
      this.error = err;
      return;
    }
    this.setupStep = 'summary';
  }

  onWantAddonsChange(v: boolean) {
    if (!v) this.selectedAddonIds = [];
  }

  onBillingCycleChange(c: string) {
    this.selectedBillingCycle = c === 'annually' ? 'annually' : 'monthly';
    this.syncPayloadFromWizard();
    if (!this.setupData.tierPriceId) {
      this.error = 'Für diese Kombination ist kein Preis hinterlegt. Bitte anderes Abo oder Laufzeit wählen.';
    } else {
      this.error = '';
    }
  }

  uniqueAddonPackages(): AddonPackageView[] {
    if (!this.setupOptions) return [];
    const byId = new Map<string, AddonPackageView>();
    for (const row of this.setupOptions.addonPrices) {
      let cur = byId.get(row.addonId);
      if (!cur) {
        const flags = this.flagsFromRow(row);
        cur = {
          addonId: row.addonId,
          addonName: row.addonName,
          isCombo: row.isCombo,
          isAllIn: this.isAllInFlags(flags),
          includesSauna: flags.sauna,
          includesSolarium: flags.solarium,
          includesDrinks: flags.drinks,
          includesCoffee: flags.coffee,
          priceMonthly: 0,
          priceAnnual: 0,
        };
        byId.set(row.addonId, cur);
      }
      if (row.billingCycle === 'monthly') cur.priceMonthly = row.amount;
      if (row.billingCycle === 'annually') cur.priceAnnual = row.amount;
    }
    return Array.from(byId.values()).sort(
      (a, b) =>
        Number(b.isAllIn) - Number(a.isAllIn) ||
        Number(b.isCombo) - Number(a.isCombo) ||
        a.addonName.localeCompare(b.addonName),
    );
  }

  private flagsFromRow(row: AddonPriceRow): AddonFeatureFlags {
    return {
      sauna: !!row.includesSauna,
      solarium: !!row.includesSolarium,
      drinks: !!row.includesDrinks,
      coffee: !!row.includesCoffee,
    };
  }

  private packageFlags(p: AddonPackageView | string): AddonFeatureFlags {
    const pkg = typeof p === 'string' ? this.uniqueAddonPackages().find((x) => x.addonId === p) : p;
    return {
      sauna: !!pkg?.includesSauna,
      solarium: !!pkg?.includesSolarium,
      drinks: !!pkg?.includesDrinks,
      coffee: !!pkg?.includesCoffee,
    };
  }

  private isAllInFlags(f: AddonFeatureFlags) {
    return f.sauna && f.solarium && f.drinks && f.coffee;
  }

  private featuresOverlap(a: AddonFeatureFlags, b: AddonFeatureFlags) {
    return (
      (a.sauna && b.sauna) ||
      (a.solarium && b.solarium) ||
      (a.drinks && b.drinks) ||
      (a.coffee && b.coffee)
    );
  }

  isAddonDisabled(p: AddonPackageView) {
    if (this.selectedAddonIds.includes(p.addonId)) return false;
    const pf = this.packageFlags(p);
    return this.selectedAddonIds.some((id) => this.featuresOverlap(pf, this.packageFlags(id)));
  }

  addonFeatureLabel(p: AddonPackageView) {
    const parts: string[] = [];
    if (p.includesSauna) parts.push('Sauna');
    if (p.includesSolarium) parts.push('Solarium');
    if (p.includesDrinks) parts.push('Getränke');
    if (p.includesCoffee) parts.push('Kaffee');
    return parts.length ? 'Enthält: ' + parts.join(', ') : '';
  }

  private findSingleByFeature(feature: keyof AddonFeatureFlags) {
    return this.uniqueAddonPackages().find((p) => {
      if (p.isCombo) return false;
      const f = this.packageFlags(p);
      const n = [f.sauna, f.solarium, f.drinks, f.coffee].filter(Boolean).length;
      return n === 1 && f[feature];
    });
  }

  private findWellnessPackage() {
    return this.uniqueAddonPackages().find(
      (p) => p.isCombo && !p.isAllIn && p.includesSauna && p.includesSolarium && !p.includesDrinks && !p.includesCoffee,
    );
  }

  private findAllInPackage() {
    return this.uniqueAddonPackages().find((p) => p.isAllIn);
  }

  toggleAddon(p: AddonPackageView, checked: boolean) {
    let next = new Set(this.selectedAddonIds);
    if (checked) {
      for (const id of [...next]) {
        if (this.featuresOverlap(this.packageFlags(id), this.packageFlags(p))) next.delete(id);
      }
      next.add(p.addonId);
    } else {
      next.delete(p.addonId);
    }
    this.selectedAddonIds = Array.from(next);
    this.syncPayloadFromWizard();
  }

  private tierPriceRow(level: string, cycle: 'monthly' | 'annually') {
    return this.setupOptions?.tierPrices.find((x) => x.accessLevel === level && x.billingCycle === cycle);
  }

  syncPayloadFromWizard() {
    if (!this.setupOptions || !this.selectedAccessLevel) return;
    const tier = this.tierPriceRow(this.selectedAccessLevel, this.selectedBillingCycle);
    if (!tier?.tierPriceId) {
      this.setupData.tierPriceId = '';
      return;
    }
    this.setupData.tierPriceId = tier.tierPriceId;

    if (!this.wantAddonPackages) {
      this.setupData.addonPriceIds = [];
      return;
    }
    const ids: string[] = [];
    for (const aid of this.selectedAddonIds) {
      const row = this.setupOptions.addonPrices.find((x) => x.addonId === aid && x.billingCycle === this.selectedBillingCycle);
      if (row?.addonPriceId) ids.push(row.addonPriceId);
    }
    this.setupData.addonPriceIds = ids;
  }

  currentTierAmount() {
    const t = this.setupOptions && this.selectedAccessLevel ? this.tierPriceRow(this.selectedAccessLevel, this.selectedBillingCycle) : null;
    return t ? this.toMonthlyRate(t.amount, t.billingCycle) : 0;
  }

  addonAmountForAddonId(addonId: string) {
    const row = this.setupOptions?.addonPrices.find((x) => x.addonId === addonId && x.billingCycle === this.selectedBillingCycle);
    return row ? this.toMonthlyRate(row.amount, row.billingCycle) : 0;
  }

  currentAddonSum() {
    if (!this.wantAddonPackages) return 0;
    return this.selectedAddonIds.reduce((s, id) => s + this.addonAmountForAddonId(id), 0);
  }

  previewGross() {
    return this.currentTierAmount() + this.currentAddonSum();
  }

  studentSavings() {
    if (!this.setupOptions?.isVerifiedStudent) return 0;
    return Math.round(this.previewGross() * 10) / 100;
  }

  bundleSavingsLines(): { label: string; amount: number }[] {
    if (!this.setupOptions || !this.wantAddonPackages) return [];
    const lines: { label: string; amount: number }[] = [];

    const allIn = this.findAllInPackage();
    if (allIn && this.selectedAddonIds.includes(allIn.addonId)) {
      const singles = (['sauna', 'solarium', 'drinks', 'coffee'] as const)
        .map((f) => this.findSingleByFeature(f))
        .filter((x): x is AddonPackageView => !!x);
      const separate = singles.reduce((s, p) => s + this.addonAmountForAddonId(p.addonId), 0);
      const save = Math.max(0, Math.round((separate - this.addonAmountForAddonId(allIn.addonId)) * 100) / 100);
      if (save > 0) lines.push({ label: 'Ersparnis All In vs. alle Einzelpakete', amount: save });
      return lines;
    }

    const wellness = this.findWellnessPackage();
    if (wellness && this.selectedAddonIds.includes(wellness.addonId)) {
      const sauna = this.findSingleByFeature('sauna');
      const solar = this.findSingleByFeature('solarium');
      if (sauna && solar) {
        const separate = this.addonAmountForAddonId(sauna.addonId) + this.addonAmountForAddonId(solar.addonId);
        const save = Math.max(0, Math.round((separate - this.addonAmountForAddonId(wellness.addonId)) * 100) / 100);
        if (save > 0) lines.push({ label: 'Ersparnis Wellness-Bundle vs. Sauna+Solarium einzeln', amount: save });
      }
    }
    return lines;
  }

  previewTotal() {
    const gross = this.previewGross();
    // Backend: student discount on gross; bundle price is already in addon sum — wellness line is informational only.
    return this.setupOptions?.isVerifiedStudent ? Math.round(gross * 90) / 100 : gross;
  }

  proratedFirstPayment() {
    const monthly = this.previewTotal();
    const now = new Date();
    const y = now.getUTCFullYear();
    const m = now.getUTCMonth();
    const day = now.getUTCDate();
    if (day === 1) return monthly;
    const daysInMonth = new Date(Date.UTC(y, m + 1, 0)).getUTCDate();
    const daysLeft = daysInMonth - day + 1;
    return Math.round(((monthly * daysLeft) / daysInMonth) * 100) / 100;
  }

  locationLabel() {
    if (!this.setupOptions) return '—';
    const flat = Object.values(this.setupOptions.locationsByCountry).flat();
    const hit = flat.find((l) => l.locationId === this.setupData.homeLocationId);
    return hit ? `${hit.city} — ${hit.name}` : '—';
  }

  selectedAddonNames() {
    if (!this.setupOptions) return [];
    return this.selectedAddonIds
      .map((id) => this.uniqueAddonPackages().find((p) => p.addonId === id)?.addonName)
      .filter((x): x is string => !!x);
  }

  finalizeSetup() {
    this.syncPayloadFromWizard();
    const err = this.validateSetupPayload();
    if (err) {
      this.error = err;
      return;
    }
    this.setupAccount();
  }

  private validateSetupPayload(): string | null {
    if (!this.setupOptions) return 'Setup-Optionen fehlen. Bitte von Schritt 1 neu starten.';
    if (!this.selectedAccessLevel) return 'Bitte ein Abo-Modell wählen (Schritt 4).';
    if (!this.setupData.homeLocationId) return 'Bitte einen Standort wählen (Schritt 3).';
    if (!this.setupData.tierPriceId) return 'Bitte Laufzeit wählen (Schritt 6), damit der Preis gesetzt wird.';
    if (!this.setupData.email?.trim() || !this.setupData.email.includes('@'))
      return 'Bitte eine gültige E-Mail angeben.';
    if (!this.setupData.dateOfBirth) return 'Bitte Geburtsdatum angeben.';
    if (!this.setupData.newUsername?.trim() || this.setupData.newUsername.trim().length < 3)
      return 'Neuer Username: mindestens 3 Zeichen.';
    return null;
  }

  private extractApiError(err: unknown): string {
    const e = err as { error?: { message?: string; Message?: string; errors?: Record<string, string[] | string> } };
    const direct = e?.error?.message ?? e?.error?.Message;
    if (typeof direct === 'string' && direct) return direct;
    const errors = e?.error?.errors;
    if (errors) {
      const parts: string[] = [];
      for (const [k, v] of Object.entries(errors)) {
        if (Array.isArray(v)) parts.push(...v);
        else if (typeof v === 'string') parts.push(v);
        else parts.push(`${k}: ungültig`);
      }
      if (parts.length) return parts.join(' ');
    }
    return 'Setup fehlgeschlagen. Prüfe alle Schritte (Standort, Abo, Laufzeit).';
  }

  login() {
    this.loading = true;
    this.error = '';

    this.http.post<any>('/api/Auth/member/login', { email: this.email, password: this.password }).subscribe({
      next: (res) => {
        this.loading = false;
        localStorage.setItem('token', res.access_token);
        this.promptSaveAndRedirect();
      },
      error: () => {
        this.loading = false;
        this.error = 'Login fehlgeschlagen. Falsches Passwort?';
      },
    });
  }

  setupAccount() {
    this.loading = true;
    this.error = '';
    this.syncPayloadFromWizard();

    this.http.post<{ message?: string }>('/api/Registration/member/setup', this.setupData).subscribe({
      next: () => {
        this.email = this.setupData.newUsername;
        this.password = this.setupData.newPassword;
        this.login();
      },
      error: (err) => {
        this.loading = false;
        this.error = this.extractApiError(err);
      },
    });
  }

  promptSaveAndRedirect() {
    if (confirm('Login-Daten für das nächste Mal merken?')) {
      console.log('Login info saved (demo)');
    }
    void this.router.navigate(['/dashboard']);
  }
}
