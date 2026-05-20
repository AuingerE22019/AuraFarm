import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpHeaders, HttpErrorResponse } from '@angular/common/http';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { finalize, timeout, TimeoutError } from 'rxjs';

@Component({
  selector: 'app-staff-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="dashboard">
      <header>
        <h1>Staff Dashboard</h1>
        <button type="button" (click)="logout()" class="btn-logout">Logout</button>
      </header>

      <main>
        <nav class="tabs" role="tablist" aria-label="Staff Tabs">
          <button type="button" class="tab" [class.active]="tab === 'members'" (click)="tab = 'members'">
            Mitglieder
          </button>
          <button type="button" class="tab" [class.active]="tab === 'assets'" (click)="openAssetsTab()">
            Räume & Geräte
          </button>
          <button type="button" class="tab" [class.active]="tab === 'courses'" (click)="openCoursesTab()">
            Kurse & Termine
          </button>
        </nav>

        <div class="page-loading" *ngIf="pageLoading" aria-live="polite">
          <div class="spinner"></div>
          <p>Lade Dashboard …</p>
        </div>

        <section *ngIf="tab === 'members'">
          <div class="actions">
            <button type="button" (click)="openRegisterModal()" class="btn-primary">+ Neues Mitglied registrieren</button>
          </div>

          <section class="recruited-members">
            <h2>Von mir rekrutierte Mitglieder</h2>
            <p *ngIf="members.length === 0">Noch keine Mitglieder rekrutiert.</p>
            <table *ngIf="members.length > 0">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Username</th>
                  <th>Registriert am</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let m of members">
                  <td>{{ m.firstName }} {{ m.lastName }}</td>
                  <td>{{ m.username }}</td>
                  <td>{{ m.registrationDate | date }}</td>
                </tr>
              </tbody>
            </table>
          </section>
        </section>

        <section *ngIf="tab === 'assets'" class="assets">
          <div class="panel">
            <div class="panel-head">
              <div>
                <h2>Standortübersicht</h2>
                <p class="muted" *ngIf="homeAssets">
                  {{ homeAssets.name }} · {{ homeAssets.city }} ({{ homeAssets.countryIso }})
                </p>
              </div>
              <button type="button" class="btn-secondary" (click)="loadAssets()" [disabled]="assetsLoading">
                Aktualisieren
              </button>
            </div>

            <div *ngIf="assetsLoading" class="muted">Lade Räume & Geräte …</div>
            <div class="error" *ngIf="assetsError">{{ assetsError }}</div>

            <div *ngIf="homeAssets?.rooms?.length">
              <div class="room" *ngFor="let r of homeAssets.rooms">
                <div class="room-head">
                  <h3>{{ r.roomName ?? 'Raum' }}</h3>
                  <p class="muted">
                    Kapazität: {{ r.maxOccupancy ?? '—' }} · Boden: {{ r.floorType ?? '—' }}
                  </p>
                </div>

                <p *ngIf="!r.equipment?.length" class="muted">Keine Geräte.</p>

                <table *ngIf="r.equipment?.length">
                  <thead>
                    <tr>
                      <th>Gerät</th>
                      <th>Seriennr.</th>
                      <th>Status</th>
                      <th>Aktion</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let e of r.equipment">
                      <td>{{ e.brandModel ?? '—' }}</td>
                      <td>{{ e.serialNumber ?? '—' }}</td>
                      <td>
                        <span class="badge" [class.badge-broken]="e.status === 'broken'"
                          [class.badge-repair]="e.status === 'under_repair'"
                          [class.badge-ok]="!e.status || e.status === 'operational'">
                          {{ e.status ?? 'operational' }}
                        </span>
                      </td>
                      <td class="actions-cell">
                        <button type="button" class="btn-mini" (click)="markEquipmentStatus(e.equipmentId, 'operational')">
                          OK
                        </button>
                        <button type="button" class="btn-mini" (click)="markEquipmentStatus(e.equipmentId, 'under_repair')">
                          Reparatur
                        </button>
                        <button type="button" class="btn-mini danger" (click)="markEquipmentStatus(e.equipmentId, 'broken')">
                          Defekt
                        </button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </section>

        <section *ngIf="tab === 'courses'" class="courses">
          <div class="panel">
            <div class="panel-head">
              <h2>Kurse (Vorlagen)</h2>
              <button type="button" class="btn-primary" (click)="openClassForm()">+ Kurs anlegen</button>
            </div>
            <div *ngIf="coursesLoading" class="muted">Lade …</div>
            <div class="error" *ngIf="coursesError">{{ coursesError }}</div>
            <table *ngIf="classList.length">
              <thead>
                <tr>
                  <th>Titel</th>
                  <th>Level</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let c of classList">
                  <td>{{ c.title }}</td>
                  <td>{{ c.difficulty }}</td>
                  <td class="actions-cell">
                    <button type="button" class="btn-mini" (click)="editClass(c)">Bearbeiten</button>
                    <button type="button" class="btn-mini danger" (click)="deleteClass(c.classId)">Löschen</button>
                  </td>
                </tr>
              </tbody>
            </table>
            <p *ngIf="!coursesLoading && !classList.length" class="muted">Noch keine Kurse.</p>
          </div>

          <div class="panel">
            <div class="panel-head">
              <h2>Termine</h2>
              <button type="button" class="btn-primary" (click)="openSessionForm()" [disabled]="!classList.length">
                + Termin anlegen
              </button>
            </div>
            <table *ngIf="sessionList.length">
              <thead>
                <tr>
                  <th>Kurs</th>
                  <th>Start</th>
                  <th>Raum</th>
                  <th>Trainer</th>
                  <th>Plätze</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let s of sessionList" [class.row-cancelled]="s.isCancelled">
                  <td>{{ s.classTitle }}</td>
                  <td>{{ formatSessionWhen(s.startTime) }}</td>
                  <td>{{ s.roomName }}</td>
                  <td>{{ s.trainerName }}</td>
                  <td>{{ s.bookedCount }}/{{ s.maxParticipants }}</td>
                  <td class="actions-cell">
                    <button type="button" class="btn-mini" (click)="editSession(s)" [disabled]="s.isCancelled">Bearbeiten</button>
                    <button type="button" class="btn-mini danger" (click)="cancelSession(s.sessionId)" [disabled]="s.isCancelled">
                      Absagen
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
            <p *ngIf="!coursesLoading && !sessionList.length" class="muted">Noch keine Termine.</p>
          </div>
        </section>
      </main>

      <div class="modal-overlay" *ngIf="classModalOpen">
        <div class="modal">
          <h2>{{ editingClassId ? 'Kurs bearbeiten' : 'Kurs anlegen' }}</h2>
          <div class="form-grid">
            <div class="form-group full-width">
              <label>Titel *</label>
              <input type="text" [(ngModel)]="classForm.title" name="ctitle" />
            </div>
            <div class="form-group full-width">
              <label>Beschreibung</label>
              <input type="text" [(ngModel)]="classForm.description" name="cdesc" />
            </div>
            <div class="form-group">
              <label>Level</label>
              <select [(ngModel)]="classForm.difficulty" name="cdiff">
                <option value="beginner">Beginner</option>
                <option value="intermediate">Intermediate</option>
                <option value="advanced">Advanced</option>
                <option value="pro">Pro</option>
              </select>
            </div>
          </div>
          <div class="modal-actions">
            <button type="button" class="btn-secondary" (click)="classModalOpen = false">Abbrechen</button>
            <button type="button" class="btn-primary" (click)="saveClass()">Speichern</button>
          </div>
        </div>
      </div>

      <div class="modal-overlay" *ngIf="sessionModalOpen">
        <div class="modal">
          <h2>{{ editingSessionId ? 'Termin bearbeiten' : 'Termin anlegen' }}</h2>
          <div class="form-grid">
            <div class="form-group full-width">
              <label>Kurs *</label>
              <select [(ngModel)]="sessionForm.classId" name="sclass">
                <option value="">— wählen —</option>
                <option *ngFor="let c of classList" [value]="c.classId">{{ c.title }}</option>
              </select>
            </div>
            <div class="form-group">
              <label>Raum *</label>
              <select [(ngModel)]="sessionForm.roomId" name="sroom">
                <option value="">— wählen —</option>
                <option *ngFor="let r of coursesMeta?.rooms ?? []" [value]="r.roomId">{{ r.roomName }}</option>
              </select>
            </div>
            <div class="form-group">
              <label>Trainer *</label>
              <select [(ngModel)]="sessionForm.trainerId" name="strainer">
                <option value="">— wählen —</option>
                <option *ngFor="let t of coursesMeta?.trainers ?? []" [value]="t.staffId">{{ t.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label>Start *</label>
              <input type="datetime-local" [(ngModel)]="sessionForm.startLocal" name="sstart" />
            </div>
            <div class="form-group">
              <label>Ende *</label>
              <input type="datetime-local" [(ngModel)]="sessionForm.endLocal" name="send" />
            </div>
            <div class="form-group">
              <label>Max. Teilnehmer</label>
              <input type="number" min="1" [(ngModel)]="sessionForm.maxParticipants" name="smax" />
            </div>
          </div>
          <div class="error" *ngIf="coursesError">{{ coursesError }}</div>
          <div class="modal-actions">
            <button type="button" class="btn-secondary" (click)="sessionModalOpen = false">Abbrechen</button>
            <button type="button" class="btn-primary" (click)="saveSession()">Speichern</button>
          </div>
        </div>
      </div>

      <div class="modal-overlay" *ngIf="showRegisterModal">
        <div class="modal modal-with-busy">
          <div class="modal-busy" *ngIf="isGeneratingPdf" aria-live="polite">
            <div class="generating-inner">
              <h2>PDF wird generiert…</h2>
              <div class="spinner"></div>
              <p>Mitglied wird angelegt, bitte warten.</p>
            </div>
          </div>

          <h2>Neues Mitglied registrieren</h2>
          <p class="hint">
            Nur Vor- und Nachname sowie Studentenstatus. Alle weiteren Daten und Notfallkontakte trägt das Mitglied nach
            dem ersten Login mit den temporären Zugangsdaten aus der PDF ein.
          </p>
          <form (ngSubmit)="registerMember()">
            <div class="form-grid">
              <div class="form-group">
                <label>Vorname *</label>
                <input type="text" [(ngModel)]="newMember.firstName" name="firstName" required />
              </div>
              <div class="form-group">
                <label>Nachname *</label>
                <input type="text" [(ngModel)]="newMember.lastName" name="lastName" required />
              </div>
              <div class="form-group full-width">
                <label class="inline-check">
                  <input type="checkbox" [(ngModel)]="newMember.isVerifiedStudent" name="student" />
                  Verifizierter Student (Studententarif)
                </label>
              </div>
            </div>

            <div class="error" *ngIf="error">{{ error }}</div>

            <div class="modal-actions">
              <button type="button" class="btn-secondary" (click)="generateTestData()" [disabled]="isGeneratingPdf">
                Testdaten
              </button>
              <div class="modal-actions-right">
                <button type="button" class="btn-secondary" (click)="closeRegisterModal()" [disabled]="isGeneratingPdf">
                  Abbrechen
                </button>
                <button type="submit" class="btn-primary" [disabled]="isGeneratingPdf">PDF erzeugen</button>
              </div>
            </div>
          </form>
        </div>
      </div>

      <div class="modal-overlay" *ngIf="pdfUrl">
        <div class="modal wide">
          <h2>Registrierung erfolgreich</h2>
          <p>PDF mit temporärem Benutzernamen und Passwort — dem Mitglied ausdrucken oder zusenden.</p>

          <iframe *ngIf="pdfSafeUrl" [src]="pdfSafeUrl" title="Setup PDF" class="pdf-frame"></iframe>

          <div class="modal-actions">
            <a [href]="pdfUrl" download="AuraFarm_Setup.pdf" class="btn-primary link-btn">PDF herunterladen</a>
            <button type="button" class="btn-secondary" (click)="closePdfModal()">Schließen</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .dashboard {
        padding: 2rem;
        max-width: 1200px;
        margin: 0 auto;
        color: var(--text);
      }
      header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 2rem;
      }
      .actions {
        margin-bottom: 2rem;
      }
      .tabs {
        display: flex;
        gap: 0.75rem;
        margin-bottom: 1rem;
      }
      .tab {
        background: var(--surface-2);
        color: var(--text);
        border: 1px solid var(--border);
        padding: 0.5rem 0.9rem;
        border-radius: 999px;
      }
      .tab.active {
        border-color: var(--border-accent);
        background: rgba(34, 211, 238, 0.08);
      }
      .panel {
        background: var(--surface-1);
        border: 1px solid var(--border);
        border-radius: var(--radius-panel);
        padding: 1.25rem;
        box-shadow: var(--shadow-panel);
      }
      .panel-head {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: center;
        flex-wrap: wrap;
      }
      .muted {
        color: var(--text-secondary);
        margin: 0.25rem 0 0;
      }
      .room {
        margin-top: 1rem;
        padding-top: 1rem;
        border-top: 1px solid var(--border);
      }
      .room-head h3 {
        margin: 0;
      }
      .actions-cell {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .btn-mini {
        padding: 0.35rem 0.6rem;
        border-radius: 6px;
        border: 1px solid var(--border);
        background: var(--surface-3);
        color: var(--text);
        cursor: pointer;
        font-weight: 700;
      }
      .btn-mini.danger {
        border-color: var(--danger-border);
        background: rgba(244, 63, 94, 0.14);
        color: var(--danger-text);
      }
      .badge {
        display: inline-block;
        padding: 0.2rem 0.55rem;
        border-radius: 999px;
        border: 1px solid var(--border);
        font-size: 0.85rem;
      }
      .badge-ok {
        border-color: rgba(34, 211, 238, 0.25);
        background: rgba(34, 211, 238, 0.08);
      }
      .badge-repair {
        border-color: rgba(167, 139, 250, 0.25);
        background: rgba(167, 139, 250, 0.08);
      }
      .badge-broken {
        border-color: var(--danger-border);
        background: var(--danger-bg);
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
      table {
        width: 100%;
        border-collapse: collapse;
        margin-top: 1rem;
        background: var(--surface-1);
        border-radius: var(--radius-panel);
        overflow: hidden;
      }
      th,
      td {
        padding: 1rem;
        text-align: left;
        border-bottom: 1px solid var(--border);
      }
      th {
        background: var(--surface-2);
        font-weight: bold;
        color: var(--text-secondary);
      }
      button {
        padding: 0.5rem 1rem;
        border: none;
        border-radius: 4px;
        cursor: pointer;
        font-weight: bold;
      }
      .btn-primary {
        background: var(--accent-cyan);
        color: #000;
      }
      .btn-secondary {
        background: var(--surface-3);
        color: var(--text);
        border: 1px solid var(--border);
      }
      .btn-logout {
        background: transparent;
        color: var(--danger-text);
        border: 1px solid var(--danger-border);
      }
      .modal-overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.7);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
      }
      .modal {
        background: var(--surface-1);
        padding: 2rem;
        border-radius: var(--radius-panel);
        width: 100%;
        max-width: 560px;
        max-height: 90vh;
        overflow-y: auto;
        border: 1px solid var(--border);
        box-shadow: var(--shadow-panel);
      }
      .modal-with-busy {
        position: relative;
      }
      .modal-busy {
        position: absolute;
        inset: 0;
        z-index: 2;
        display: flex;
        align-items: center;
        justify-content: center;
        background: color-mix(in srgb, var(--surface-1) 88%, transparent);
        border-radius: var(--radius-panel);
        padding: 1rem;
      }
      .generating-inner {
        text-align: center;
        max-width: 360px;
      }
      .modal.wide {
        max-width: 840px;
      }
      .hint {
        color: var(--text-secondary);
        font-size: 0.9rem;
        margin-bottom: 1rem;
      }
      .form-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1rem;
      }
      .form-group.full-width {
        grid-column: 1 / -1;
      }
      .inline-check {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-weight: 600;
        cursor: pointer;
      }
      .form-group label {
        display: block;
        margin-bottom: 0.25rem;
        font-weight: bold;
        color: var(--text-secondary);
      }
      .form-group input[type='text'] {
        width: 100%;
        padding: 0.5rem;
        border: 1px solid var(--border-strong);
        border-radius: 4px;
        box-sizing: border-box;
        background: var(--void-0);
        color: var(--text);
      }
      .modal-actions {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 1rem;
        margin-top: 1rem;
      }
      .modal-actions-right {
        display: flex;
        gap: 1rem;
      }
      .error {
        color: var(--danger-text);
        background: var(--danger-bg);
        border: 1px solid var(--danger-border);
        padding: 0.5rem;
        border-radius: 4px;
        margin: 1rem 0;
      }
      .spinner {
        border: 4px solid var(--surface-3);
        border-top: 4px solid var(--accent-cyan);
        border-radius: 50%;
        width: 40px;
        height: 40px;
        animation: spin 1s linear infinite;
        margin: 2rem auto;
      }
      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
      .pdf-frame {
        width: 100%;
        height: 420px;
        border: 1px solid var(--border);
        margin: 1rem 0;
        background: #fff;
      }
      .link-btn {
        text-decoration: none;
        display: inline-block;
        padding: 0.75rem 1rem;
      }
      .courses .panel {
        margin-bottom: 1.5rem;
      }
      .row-cancelled {
        opacity: 0.55;
      }
      .form-group select {
        width: 100%;
        padding: 0.5rem;
        border: 1px solid var(--border-strong);
        border-radius: 4px;
        background: var(--void-0);
        color: var(--text);
      }
      .form-group.full-width {
        grid-column: 1 / -1;
      }
    `,
  ],
})
export class StaffDashboardComponent implements OnInit {
  members: any[] = [];
  pageLoading = true;
  tab: 'members' | 'assets' | 'courses' = 'members';

  showRegisterModal = false;
  isGeneratingPdf = false;

  error = '';
  newMember: { firstName: string; lastName: string; isVerifiedStudent: boolean } = {
    firstName: '',
    lastName: '',
    isVerifiedStudent: false,
  };

  pdfUrl: string | null = null;
  pdfSafeUrl: SafeResourceUrl | null = null;

  assetsLoading = false;
  assetsError = '';
  homeAssets: any | null = null;

  coursesLoading = false;
  coursesError = '';
  coursesMeta: any | null = null;
  classList: any[] = [];
  sessionList: any[] = [];
  classModalOpen = false;
  sessionModalOpen = false;
  editingClassId: string | null = null;
  editingSessionId: string | null = null;
  classForm = { title: '', description: '', difficulty: 'beginner' };
  sessionForm = {
    classId: '',
    roomId: '',
    trainerId: '',
    startLocal: '',
    endLocal: '',
    maxParticipants: 10,
  };

  constructor(
    private http: HttpClient,
    private sanitizer: DomSanitizer,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit() {
    this.loadMembers();
  }

  getHeaders() {
    return new HttpHeaders().set('Authorization', 'Bearer ' + localStorage.getItem('token'));
  }

  loadMembers() {
    this.pageLoading = true;
    this.http
      .get<any[]>('/api/Members', { headers: this.getHeaders() })
      .pipe(
        timeout(15_000),
        finalize(() => {
          this.pageLoading = false;
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: (data) => {
          this.members = data;
        },
        error: (err) => {
          console.error('Failed to load members', err);
          this.members = [];
        },
      });
  }

  loadAssets() {
    this.assetsLoading = true;
    this.assetsError = '';
    this.http
      .get('/api/StaffAssets/home', { headers: this.getHeaders() })
      .pipe(
        timeout(15_000),
        finalize(() => {
          this.assetsLoading = false;
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: (data) => {
          this.homeAssets = data;
        },
        error: (err: any) => {
          this.homeAssets = null;
          this.assetsError = err?.error?.message ?? 'Konnte Standortdaten nicht laden.';
        },
      });
  }

  openAssetsTab() {
    this.tab = 'assets';
    if (!this.homeAssets && !this.assetsLoading) this.loadAssets();
  }

  openCoursesTab() {
    this.tab = 'courses';
    this.loadCoursesData();
  }

  loadCoursesData() {
    this.coursesLoading = true;
    this.coursesError = '';
    this.http
      .get<any>('/api/StaffClasses/meta', { headers: this.getHeaders() })
      .pipe(
        timeout(15_000),
        finalize(() => {
          this.coursesLoading = false;
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: (meta) => {
          this.coursesMeta = meta;
          this.classList = meta.classes ?? [];
          this.loadSessions();
        },
        error: (err: any) => {
          this.coursesError = err?.error?.message ?? 'Kurse konnten nicht geladen werden.';
        },
      });
  }

  loadSessions() {
    this.http
      .get<any[]>('/api/StaffClasses/sessions?upcomingOnly=true', { headers: this.getHeaders() })
      .subscribe({
        next: (rows) => {
          this.sessionList = rows;
          this.cdr.detectChanges();
        },
        error: () => {
          this.sessionList = [];
        },
      });
  }

  formatSessionWhen(iso: string) {
    const d = new Date(iso);
    return d.toLocaleString('de-AT', { dateStyle: 'short', timeStyle: 'short' });
  }

  openClassForm() {
    this.editingClassId = null;
    this.classForm = { title: '', description: '', difficulty: 'beginner' };
    this.classModalOpen = true;
  }

  editClass(c: any) {
    this.editingClassId = c.classId;
    this.classForm = { title: c.title, description: c.description ?? '', difficulty: c.difficulty ?? 'beginner' };
    this.classModalOpen = true;
  }

  saveClass() {
    const body = this.classForm;
    const req = this.editingClassId
      ? this.http.put(`/api/StaffClasses/classes/${this.editingClassId}`, body, { headers: this.getHeaders() })
      : this.http.post('/api/StaffClasses/classes', body, { headers: this.getHeaders() });
    req.subscribe({
      next: () => {
        this.classModalOpen = false;
        this.loadCoursesData();
      },
      error: (err: any) => {
        this.coursesError = err?.error?.message ?? 'Speichern fehlgeschlagen.';
      },
    });
  }

  deleteClass(id: string) {
    if (!confirm('Kurs wirklich löschen?')) return;
    this.http.delete(`/api/StaffClasses/classes/${id}`, { headers: this.getHeaders() }).subscribe({
      next: () => this.loadCoursesData(),
      error: (err: any) => {
        this.coursesError = err?.error?.message ?? 'Löschen fehlgeschlagen.';
      },
    });
  }

  openSessionForm() {
    this.editingSessionId = null;
    const start = new Date();
    start.setHours(start.getHours() + 24, 0, 0, 0);
    const end = new Date(start.getTime() + 60 * 60 * 1000);
    this.sessionForm = {
      classId: this.classList[0]?.classId ?? '',
      roomId: this.coursesMeta?.rooms?.[0]?.roomId ?? '',
      trainerId: this.coursesMeta?.trainers?.[0]?.staffId ?? '',
      startLocal: this.toLocalInput(start),
      endLocal: this.toLocalInput(end),
      maxParticipants: 10,
    };
    this.sessionModalOpen = true;
  }

  editSession(s: any) {
    this.editingSessionId = s.sessionId;
    this.sessionForm = {
      classId: s.classId,
      roomId: s.roomId,
      trainerId: s.trainerId,
      startLocal: this.toLocalInput(new Date(s.startTime)),
      endLocal: this.toLocalInput(new Date(s.endTime)),
      maxParticipants: s.maxParticipants,
    };
    this.sessionModalOpen = true;
  }

  toLocalInput(d: Date) {
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  saveSession() {
    const body = {
      classId: this.sessionForm.classId,
      roomId: this.sessionForm.roomId,
      trainerId: this.sessionForm.trainerId,
      startTime: new Date(this.sessionForm.startLocal).toISOString(),
      endTime: new Date(this.sessionForm.endLocal).toISOString(),
      maxParticipants: Number(this.sessionForm.maxParticipants) || 10,
    };
    const req = this.editingSessionId
      ? this.http.put(`/api/StaffClasses/sessions/${this.editingSessionId}`, body, { headers: this.getHeaders() })
      : this.http.post('/api/StaffClasses/sessions', body, { headers: this.getHeaders() });
    req.subscribe({
      next: () => {
        this.sessionModalOpen = false;
        this.loadSessions();
      },
      error: (err: any) => {
        this.coursesError = err?.error?.message ?? 'Termin konnte nicht gespeichert werden.';
      },
    });
  }

  cancelSession(id: string) {
    if (!confirm('Termin absagen? Eingetragene Mitglieder werden abgemeldet.')) return;
    this.http.post(`/api/StaffClasses/sessions/${id}/cancel`, {}, { headers: this.getHeaders() }).subscribe({
      next: () => this.loadSessions(),
      error: (err: any) => {
        this.coursesError = err?.error?.message ?? 'Absage fehlgeschlagen.';
      },
    });
  }

  markEquipmentStatus(equipmentId: string, status: 'operational' | 'under_repair' | 'broken' | 'retired') {
    this.assetsError = '';
    this.http
      .patch(
        `/api/StaffAssets/equipment/${equipmentId}/status`,
        { status },
        { headers: this.getHeaders() },
      )
      .pipe(timeout(15_000))
      .subscribe({
        next: () => this.loadAssets(),
        error: (err: any) => {
          this.assetsError = err?.error?.message ?? 'Status konnte nicht gespeichert werden.';
        },
      });
  }

  openRegisterModal() {
    this.error = '';
    this.newMember = { firstName: '', lastName: '', isVerifiedStudent: false };
    this.showRegisterModal = true;
  }

  closeRegisterModal() {
    this.showRegisterModal = false;
    this.error = '';
  }

  generateTestData() {
    const r = Math.floor(Math.random() * 10000);
    this.newMember = {
      firstName: 'Test',
      lastName: 'Mitglied' + r,
      isVerifiedStudent: true,
    };
  }

  registerMember() {
    this.error = '';
    this.isGeneratingPdf = true;

    this.http
      .post('/api/Registration/staff/members', this.newMember, {
        headers: this.getHeaders(),
        responseType: 'blob',
        observe: 'response',
      })
      .pipe(
        timeout(90_000),
        finalize(() => {
          this.isGeneratingPdf = false;
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: async (resp) => {
          const blob = resp.body;
          if (!blob || blob.size === 0) {
            await this.applyBlobError(blob ?? new Blob());
            return;
          }

          const ct = (resp.headers.get('content-type') ?? '').toLowerCase();
          if (ct.includes('application/json') || ct.includes('text/')) {
            await this.applyBlobError(blob);
            return;
          }

          this.showRegisterModal = false;
          this.newMember = { firstName: '', lastName: '', isVerifiedStudent: false };
          this.loadMembers();

          if (this.pdfUrl) window.URL.revokeObjectURL(this.pdfUrl);
          this.pdfUrl = window.URL.createObjectURL(blob);
          this.pdfSafeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.pdfUrl);
          this.cdr.detectChanges();
        },
        error: (err: unknown) => {
          void this.handleRegisterAnyError(err);
        },
      });
  }

  private async handleRegisterAnyError(err: unknown) {
    if (err instanceof TimeoutError) {
      this.error =
        'Zeitüberschreitung (90s). Bitte prüfen: läuft die API (z. B. http://localhost:5023) und ist der Dev-Server-Proxy aktiv?';
      this.showRegisterModal = true;
      this.cdr.detectChanges();
      return;
    }
    if (err instanceof HttpErrorResponse) {
      await this.handleRegisterHttpError(err);
      return;
    }
    this.error = 'Registrierung fehlgeschlagen (unbekannter Fehler).';
    this.showRegisterModal = true;
    this.cdr.detectChanges();
  }

  private async handleRegisterHttpError(err: HttpErrorResponse) {
    const body = err.error;
    if (body instanceof Blob) {
      await this.applyBlobError(body);
      return;
    }
    if (body && typeof body === 'object' && 'message' in body) {
      this.error = String((body as { message: unknown }).message);
    } else {
      this.error = err.message || `Registrierung fehlgeschlagen (${err.status}).`;
    }
    this.showRegisterModal = true;
    this.cdr.detectChanges();
  }

  private async applyBlobError(blob: Blob) {
    try {
      const t = await blob.text();
      try {
        const j = JSON.parse(t) as { message?: string; title?: string; errors?: unknown };
        this.error =
          (j.message ??
            j.title ??
            (j.errors ? JSON.stringify(j.errors) : null) ??
            t) || 'Registrierung fehlgeschlagen.';
      } catch {
        this.error = t || 'Registrierung fehlgeschlagen.';
      }
    } catch {
      this.error = 'Registrierung fehlgeschlagen.';
    }
    this.showRegisterModal = true;
    this.cdr.detectChanges();
  }

  closePdfModal() {
    if (this.pdfUrl) window.URL.revokeObjectURL(this.pdfUrl);
    this.pdfUrl = null;
    this.pdfSafeUrl = null;
  }

  logout() {
    localStorage.removeItem('token');
    window.location.href = '/staff/login';
  }
}
