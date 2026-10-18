import { ChangeDetectionStrategy, Component, inject, input, signal, effect, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsButtonDirective, ScsCardComponent, ScsEmptyStateComponent, ScsSkeletonComponent } from 'ui';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { ClinicSettings, ClinicsService } from './clinics.service';

/**
 * The clinic's «الإعدادات» tab (spec 015, User Story 5; FR-023, FR-024; frames `2589:4185`/`2589:4007`,
 * view only `2790:8210`/`2790:7655`): a read-only mirror of the clinic's own settings. Its strip says
 * who may change what (the BRD's corrected words, without the time zone), and the «الإعدادات العامة»
 * card comes now; the other cards come with the specs that make those settings.
 */
@Component({
  selector: 'app-clinic-settings-tab',
  imports: [TranslocoPipe, RouterLink, ScsButtonDirective, ScsCardComponent, ScsEmptyStateComponent, ScsSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--scs-spacing-md);
    }
    /* The frames' read-only strip: the brand's light tint, full width (2589:4185). */
    .strip {
      margin: 0;
      padding: var(--scs-spacing-sm) var(--scs-spacing-md);
      border-radius: var(--scs-radius-md, 10px);
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-active);
      font-size: var(--scs-text-body-sm);
      line-height: 1.7;
    }
    .card-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding-block-end: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle, var(--scs-color-border-default));
    }
    h2 {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 600;
    }
    .edit {
      color: var(--scs-color-brand-default);
      font-size: var(--scs-text-body-sm);
      text-decoration: none;
    }
    dl {
      margin: 0;
    }
    .row {
      display: grid;
      grid-template-columns: 260px minmax(0, 1fr);
      gap: var(--scs-spacing-md);
      padding-block: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle, var(--scs-color-border-default));
    }
    .row:last-child {
      border-block-end: 0;
      padding-block-end: 0;
    }
    dt {
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
    }
    dd {
      margin: 0;
      color: var(--scs-color-text-primary);
      font-size: var(--scs-text-body-sm);
      overflow-wrap: anywhere;
    }
    @media (max-width: 720px) {
      .row {
        grid-template-columns: minmax(0, 1fr);
        gap: var(--scs-spacing-2);
      }
    }
  `,
  template: `
    @switch (state()) {
      @case ('loading') {
        <scs-card><scs-skeleton data-testid="settings-loading" [rows]="4" [label]="'app.clinics.settings.loading' | transloco" /></scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state context="error" data-testid="settings-error" [title]="'app.clinics.detail.failedTitle' | transloco" [text]="'app.clinics.settings.failed' | transloco">
            <button type="button" scsButton data-testid="settings-retry" (click)="load()">{{ 'app.clinics.retry' | transloco }}</button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (settings(); as view) {
          <p class="strip" role="note" data-testid="settings-strip">
            {{ (edits(view) ? 'app.clinics.settings.stripEditor' : 'app.clinics.settings.stripViewer') | transloco }}
          </p>
          <scs-card data-testid="settings-general">
            <div class="card-head">
              <h2>{{ 'app.clinics.settings.general' | transloco }}</h2>
              @if (edits(view)) {
                <a class="edit" data-testid="settings-edit" [routerLink]="['/clinics', clinicId(), 'edit']">{{ 'app.clinics.settings.editLink' | transloco }}</a>
              }
            </div>
            <dl>
              <div class="row">
                <dt>{{ 'app.clinics.settings.language' | transloco }}</dt>
                <dd data-testid="settings-language">{{ 'app.clinics.form.languages.' + view.defaultLanguage | transloco }}</dd>
              </div>
              <div class="row">
                <dt>{{ 'app.clinics.settings.timeZone' | transloco }}</dt>
                <dd data-testid="settings-time-zone">{{ 'app.clinics.settings.cairo' | transloco }}</dd>
              </div>
              <div class="row">
                <dt>{{ 'app.clinics.settings.staffAddress' | transloco }}</dt>
                <dd data-testid="settings-staff-address"><bdi dir="ltr">{{ view.staffAddress }}</bdi></dd>
              </div>
            </dl>
          </scs-card>
        }
      }
    }
  `,
})
export class ClinicSettingsTab {
  private readonly clinics = inject(ClinicsService);
  private readonly access = inject(OpsAccess);

  readonly clinicId = input.required<string>();

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly settings = signal<ClinicSettings | null>(null);

  constructor() {
    // Only the clinic's id reloads: what the load itself reads and writes is not followed, or each
    // answer would ask again.
    effect(() => {
      this.clinicId();
      untracked(() => void this.load());
    });
  }

  /** The strip's editor words and the link: the platform's answer and the session's codes agree. */
  protected edits(view: ClinicSettings): boolean {
    return view.can.edit && this.access.can(OpsCan.clinicsEdit);
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    try {
      this.settings.set(await this.clinics.settings(this.clinicId()));
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }
}
