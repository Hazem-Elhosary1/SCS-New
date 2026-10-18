import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService, OnlineService } from 'core';
import { filter, map, startWith } from 'rxjs';
import {
  ScsBadgeComponent,
  ScsBreadcrumbComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsCopyButtonComponent,
  ScsEmptyStateComponent,
  ScsIconComponent,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
  ScsTab,
  ScsTabsComponent,
  ScsWriteDirective,
  ToastService,
} from 'ui';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { opsCairoDate, opsCairoDateTime, opsRelative } from '../i18n/ops-format';
import { countWords } from '../operators/count-words';
import { codeOf } from '../operators/operators.service';
import { ClinicSettingsTab } from './clinic-settings.tab';
import { clinicName, detailMark, doctorsAndBranches, planWords, statusWord } from './clinic-words';
import { ClinicCodes, ClinicDetail, ClinicsService } from './clinics.service';

/**
 * A clinic (spec 015, User Story 3; FR-014 – FR-019; frames `632:956`/`613:872`, loading `636:992`,
 * error `637:1012`, view only `2786:26469`, the sign-in address row `2330:3095`/`2330:3050`): the
 * head, the tabs «نظرة عامة» and «الإعدادات», and the «معلومات العيادة» card — its account manager
 * assigned in place, the sign-in address copied, the owner's invitation sent again. The other tabs
 * and cards come with their specs. A clinic that is not there reads as one that could not be read.
 */
@Component({
  selector: 'app-clinic-detail-page',
  imports: [
    TranslocoPipe,
    ScsBreadcrumbComponent,
    ScsTabsComponent,
    ScsCardComponent,
    ScsBadgeComponent,
    ScsButtonDirective,
    ScsWriteDirective,
    ScsIconComponent,
    ScsSelectComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    ScsCopyButtonComponent,
    ClinicSettingsTab,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'scs-form--template' },
  styles: `
    :host {
      display: block;
    }
    .head {
      display: flex;
      align-items: flex-start;
      gap: var(--scs-spacing-md);
      margin-block: var(--scs-spacing-md) var(--scs-spacing-lg);
    }
    .mark {
      display: inline-grid;
      place-items: center;
      flex: none;
      inline-size: 64px;
      block-size: 64px;
      border-radius: 50%;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-active);
      font-size: 20px;
      font-weight: 700;
    }
    .who {
      flex: 1 1 auto;
      min-inline-size: 0;
    }
    .title {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-12);
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-title-sm);
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .line {
      margin: var(--scs-spacing-xs) 0 0;
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
    }
    .head button {
      flex: none;
    }
    scs-tabs {
      display: block;
      margin-block-end: var(--scs-spacing-lg);
    }
    .info {
      display: block;
      max-inline-size: 560px;
    }
    dl {
      margin: 0;
    }
    .row {
      padding-block: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle, var(--scs-color-border-default));
    }
    .row:last-child {
      border-block-end: 0;
      padding-block-end: 0;
    }
    dt {
      margin-block-end: var(--scs-spacing-xs);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-label);
    }
    dd {
      margin: 0;
      color: var(--scs-color-text-primary);
      font-size: var(--scs-text-body-sm);
      overflow-wrap: anywhere;
    }
    .inline {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-sm);
    }
    .small {
      min-block-size: 28px;
      padding-block: 0;
      padding-inline: var(--scs-spacing-md);
      font-size: var(--scs-text-label);
    }
    .assign {
      display: grid;
      gap: var(--scs-spacing-sm);
    }
    .assign-buttons {
      display: flex;
      gap: var(--scs-spacing-sm);
    }
    .invitation {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-12);
    }
    .invitation-line {
      inline-size: 100%;
      margin: var(--scs-spacing-xs) 0 0;
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-label);
    }
    .resend {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
    }
    @media (max-width: 720px) {
      .head {
        flex-wrap: wrap;
      }
      .head button {
        inline-size: 100%;
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.clinics.form.crumb' | transloco"
      [crumbs]="clinic() ? [{ label: 'app.clinics.form.crumb' | transloco, link: '/clinics' }, { label: name(clinic()!) }] : [{ label: 'app.clinics.form.crumb' | transloco, link: '/clinics' }]"
    />

    @switch (state()) {
      @case ('loading') {
        <div class="head" data-testid="clinic-loading">
          <scs-skeleton [rows]="2" [label]="'app.clinics.detail.loading' | transloco" />
        </div>
        <scs-card class="info"><scs-skeleton [rows]="8" /></scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state
            context="error"
            data-testid="clinic-error"
            [title]="'app.clinics.detail.failedTitle' | transloco"
            [text]="'app.clinics.detail.failedText' | transloco"
          >
            <button type="button" scsButton data-testid="clinic-retry" (click)="load()">{{ 'app.clinics.retry' | transloco }}</button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (clinic(); as view) {
          <div class="head">
            <span class="mark" aria-hidden="true" data-testid="clinic-detail-mark">{{ mark(view) }}</span>
            <div class="who">
              <div class="title">
                <h1 data-testid="clinic-detail-name"><bdi>{{ name(view) }}</bdi></h1>
                <scs-badge dot [intent]="view.status.tone" data-testid="clinic-detail-status">{{ status(view) }}</scs-badge>
              </div>
              <p class="line" data-testid="clinic-detail-line"><bdi>{{ headLine(view) }}</bdi></p>
            </div>
            @if (edits()) {
              <button type="button" scsButton data-testid="clinic-edit" [scsWrite]="online()" (click)="go('/clinics/' + view.id + '/edit')">
                {{ 'app.clinics.detail.edit' | transloco }}
              </button>
            }
          </div>

          <scs-tabs
            role="tablist"
            [attr.aria-label]="'app.clinics.detail.info' | transloco"
            [tabs]="tabs()"
            [active]="tab()"
            (activeChange)="openTab($event)"
          />

          @if (tab() === 'settings') {
            <app-clinic-settings-tab [clinicId]="view.id" />
          } @else {
            <scs-card class="info" data-testid="clinic-info" [heading]="'app.clinics.detail.info' | transloco">
              <dl>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.email' | transloco }}</dt>
                  <dd data-testid="clinic-info-email"><bdi dir="ltr">{{ view.owner?.email ?? none() }}</bdi></dd>
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.phone' | transloco }}</dt>
                  <dd data-testid="clinic-info-phone"><bdi dir="ltr">{{ view.owner?.mobile ?? none() }}</bdi></dd>
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.address' | transloco }}</dt>
                  <dd data-testid="clinic-info-address"><bdi>{{ view.address ?? none() }}</bdi></dd>
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.tax' | transloco }}</dt>
                  <dd data-testid="clinic-info-tax"><bdi dir="ltr">{{ view.taxNumber ?? none() }}</bdi></dd>
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.plan' | transloco }}</dt>
                  <dd data-testid="clinic-info-plan"><bdi>{{ view.plan ? name(view.plan) : none() }}</bdi></dd>
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.cycle' | transloco }}</dt>
                  <dd data-testid="clinic-info-cycle">{{ none() }}</dd>
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.renewal' | transloco }}</dt>
                  <dd data-testid="clinic-info-renewal">{{ view.nextRenewal ? date(view.nextRenewal) : none() }}</dd>
                </div>
                <div class="row" data-testid="clinic-info-sign-in">
                  <dt>{{ 'app.clinics.detail.rows.signIn' | transloco }}</dt>
                  <dd>
                    <span class="inline">
                      <bdi dir="ltr" data-testid="clinic-sign-in-address">{{ view.signInAddress }}</bdi>
                      <scs-copy-button
                        testId="clinic-copy"
                        [value]="view.signInAddress"
                        [label]="'app.clinics.detail.copy' | transloco"
                        [copiedText]="'app.clinics.detail.copied' | transloco"
                        [failedText]="'app.clinics.detail.copyFailed' | transloco"
                      />
                    </span>
                    @if (view.owner?.invitation; as invitation) {
                      <div class="invitation" data-testid="clinic-invitation" [attr.data-state]="invitation.state">
                        <scs-badge dot [intent]="invitationTone(invitation.state)" data-testid="clinic-invitation-badge">
                          {{ 'app.clinics.detail.invitation.badge.' + invitation.state | transloco }}
                        </scs-badge>
                        @if (edits()) {
                          <button
                            type="button"
                            class="small resend"
                            data-testid="clinic-invite-resend"
                            [scsButton]="invitation.state === 'live' ? 'secondary' : 'primary'"
                            [scsWrite]="online()"
                            [busy]="sending()"
                            [disabled]="sending()"
                            (click)="resend(view)"
                          >
                            <scs-icon name="send" [size]="14" />
                            <span>{{ (sending() ? 'app.clinics.detail.invitation.resending' : 'app.clinics.detail.invitation.resend') | transloco }}</span>
                          </button>
                        }
                        <p class="invitation-line" data-testid="clinic-invitation-line">{{ invitationLine(invitation) }}</p>
                      </div>
                    }
                  </dd>
                </div>
                <div class="row" data-testid="clinic-info-manager">
                  @if (assigning()) {
                    <dt>{{ 'app.clinics.detail.rows.manager' | transloco }}</dt>
                    <dd class="assign">
                      <scs-select
                        testId="clinic-manager-select"
                        labelHidden
                        [placeholder]="'app.clinics.detail.unassigned' | transloco"
                        [label]="'app.clinics.detail.rows.manager' | transloco"
                        [options]="managerOptions()"
                        [(value)]="chosenManager"
                      />
                      <span class="assign-buttons">
                        <button type="button" scsButton class="small" data-testid="clinic-manager-save" [scsWrite]="online()" [busy]="savingManager()" [disabled]="savingManager()" (click)="saveManager(view)">
                          {{ (savingManager() ? 'app.clinics.detail.saving' : 'app.clinics.detail.save') | transloco }}
                        </button>
                        <button type="button" scsButton="secondary" class="small" data-testid="clinic-manager-cancel" (click)="assigning.set(false)">
                          {{ 'app.clinics.detail.cancel' | transloco }}
                        </button>
                      </span>
                    </dd>
                  } @else {
                    <dt>{{ 'app.clinics.detail.rows.manager' | transloco }}</dt>
                    <dd class="inline">
                      <bdi data-testid="clinic-manager-name">{{ view.accountManager?.name ?? ('app.clinics.detail.unassigned' | transloco) }}</bdi>
                      @if (edits()) {
                        <button type="button" scsButton="secondary" class="small" data-testid="clinic-manager-assign" [scsWrite]="online()" (click)="startAssign(view)">
                          {{ 'app.clinics.detail.assign' | transloco }}
                        </button>
                      }
                    </dd>
                  }
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.team' | transloco }}</dt>
                  <dd data-testid="clinic-info-team">{{ team(view) }}</dd>
                </div>
                <div class="row">
                  <dt>{{ 'app.clinics.detail.rows.lastSignIn' | transloco }}</dt>
                  <dd data-testid="clinic-info-last-sign-in">
                    {{ view.owner?.lastSignInAt ? dateTime(view.owner!.lastSignInAt!) : ('app.clinics.detail.never' | transloco) }}
                  </dd>
                </div>
              </dl>
            </scs-card>
          }
        }
      }
    }
  `,
})
export class ClinicDetailPage {
  private readonly clinics = inject(ClinicsService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly translations = inject(TranslocoService);
  private readonly access = inject(OpsAccess);
  private readonly toasts = inject(ToastService);
  private readonly spoken = toSignal(this.translations.selectTranslation(), { initialValue: null });
  private readonly params = toSignal(inject(ActivatedRoute).paramMap, { requireSync: true });
  protected readonly online = inject(OnlineService).online;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly clinic = signal<ClinicDetail | null>(null);
  protected readonly assigning = signal(false);
  protected readonly chosenManager = signal('');
  protected readonly savingManager = signal(false);
  protected readonly sending = signal(false);
  private readonly now = signal(Date.now());

  protected readonly id = computed(() => this.params().get('id') ?? '');
  protected readonly tab = computed(() => (this.url().split('?')[0].endsWith('/settings') ? 'settings' : 'overview'));

  protected readonly tabs = computed<readonly ScsTab[]>(() => {
    this.spoken();
    return [
      { key: 'overview', label: this.translations.translate('app.clinics.detail.tabs.overview') },
      { key: 'settings', label: this.translations.translate('app.clinics.detail.tabs.settings') },
    ];
  });

  /** Whoever may edit clinics, by the platform's answer and the session's codes (FR-001: removed, not disabled). */
  protected readonly edits = computed(() => (this.clinic()?.can.edit ?? false) && this.access.can(OpsCan.clinicsEdit));

  protected readonly managerOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    const clinic = this.clinic();
    // «غير معيّن» is the select's own nothing-chosen row (its placeholder), so it also reads when nothing is chosen.
    return (clinic?.managers ?? []).map((manager) => ({ value: manager.id, label: manager.name }));
  });

  constructor() {
    // Only the clinic's id reloads: what the load itself reads and writes is not followed, or each
    // answer would ask again.
    effect(() => {
      this.id();
      untracked(() => void this.load());
    });
  }

  protected name(clinic: { nameAr: string; nameEn: string }): string {
    return clinicName(clinic, this.language.language());
  }

  protected mark(clinic: ClinicDetail): string {
    return detailMark(clinic);
  }

  protected status(clinic: ClinicDetail): string {
    return statusWord(clinic.status, this.language.language());
  }

  protected none(): string {
    this.spoken();
    return this.translations.translate('app.clinics.none');
  }

  protected date(value: string): string {
    return opsCairoDate(value, this.language.language());
  }

  /** «آخر تسجيل دخول» as a Cairo date and time (contracts/clinics-screens.md). */
  protected dateTime(value: string): string {
    return opsCairoDateTime(value, this.language.language());
  }

  protected relative(value: string): string {
    return opsRelative(value, this.now(), this.language.language());
  }

  protected team(clinic: ClinicDetail): string {
    this.spoken();
    return doctorsAndBranches(this.translations, clinic.doctors, clinic.branches, this.language.language());
  }

  /** «جهة الاتصال: … · تاريخ الانضمام … · باقة …»: what the clinic lacks is left out. */
  protected headLine(clinic: ClinicDetail): string {
    this.spoken();
    const t = (key: string, values: Record<string, string>) => this.translations.translate(key, values);
    const parts = [
      clinic.owner?.name ? t('app.clinics.detail.contact', { name: clinic.owner.name }) : null,
      t('app.clinics.detail.joined', { date: this.date(clinic.createdAt) }),
      clinic.plan ? planWords(this.translations, clinic.plan, this.language.language()) : null,
    ];
    return parts.filter((part) => part !== null).join(' · ');
  }

  protected invitationTone(state: string): 'warning' | 'danger' | 'neutral' {
    return state === 'live' ? 'warning' : state === 'lapsed' ? 'danger' : 'neutral';
  }

  protected invitationLine(invitation: { state: string; at: string | null }): string {
    this.spoken();
    const language = this.language.language();
    if (invitation.state === 'live' && invitation.at !== null) {
      const hours = Math.max(0, Math.ceil((new Date(invitation.at).getTime() - this.now()) / 3_600_000));
      return this.translations.translate('app.clinics.detail.invitation.live', {
        at: opsCairoDateTime(invitation.at, language),
        left: countWords(this.translations, 'app.clinics.hours', hours, language),
      });
    }
    if (invitation.state === 'lapsed' && invitation.at !== null) {
      return this.translations.translate('app.clinics.detail.invitation.lapsed', { at: opsCairoDateTime(invitation.at, language) });
    }
    return this.translations.translate('app.clinics.detail.invitation.none');
  }

  protected async load(): Promise<void> {
    const id = this.id();
    if (this.clinic()?.id !== id) {
      this.state.set('loading');
    }
    try {
      this.clinic.set(await this.clinics.read(id));
      this.now.set(Date.now());
      this.state.set('ready');
    } catch {
      // Not there and could not be read answer alike (FR-019).
      this.clinic.set(null);
      this.state.set('failed');
    }
  }

  protected openTab(tab: string): void {
    void this.router.navigateByUrl(tab === 'settings' ? `/clinics/${this.id()}/settings` : `/clinics/${this.id()}`);
  }

  protected startAssign(clinic: ClinicDetail): void {
    // A manager who is no longer offered (suspended since) is not preselected.
    const current = clinic.accountManager?.id ?? '';
    this.chosenManager.set(clinic.managers.some((m) => m.id === current) ? current : '');
    this.assigning.set(true);
  }

  protected async saveManager(clinic: ClinicDetail): Promise<void> {
    this.savingManager.set(true);
    try {
      const saved = await this.clinics.assign(clinic.id, clinic.version, this.chosenManager() || null);
      this.clinic.set({ ...clinic, accountManager: saved.accountManager, version: saved.version });
      this.assigning.set(false);
      this.toasts.show(this.translations.translate('app.clinics.detail.managerSaved'));
    } catch (error) {
      if (codeOf(error) === ClinicCodes.changedElsewhere) {
        // Saving again cannot succeed on the old version: the clinic is read again.
        this.assigning.set(false);
        this.toasts.show(this.translations.translate('app.clinics.detail.changedElsewhere'), '', 'danger');
        await this.load();
        return;
      }
      this.toasts.show(this.translations.translate('app.clinics.detail.managerFailed'), '', 'danger');
    } finally {
      this.savingManager.set(false);
    }
  }

  protected async resend(clinic: ClinicDetail): Promise<void> {
    this.sending.set(true);
    try {
      const sent = await this.clinics.invite(clinic.id);
      this.toasts.show(
        this.translations.translate('app.clinics.detail.invitation.resent', {
          at: opsCairoDateTime(sent.expiresAt, this.language.language()),
        }),
      );
      await this.load();
    } catch (error) {
      const code = codeOf(error);
      const key =
        code === ClinicCodes.cooldown
          ? 'cooldown'
          : code === ClinicCodes.notSent
            ? 'notSent'
            : code === ClinicCodes.accepted
              ? 'accepted'
              : 'failed';
      this.toasts.show(this.translations.translate(`app.clinics.detail.invitation.${key}`), '', 'danger');
      if (code === ClinicCodes.accepted) {
        await this.load();
      }
    } finally {
      this.sending.set(false);
    }
  }

  protected go(path: string): void {
    void this.router.navigateByUrl(path);
  }
}
