import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ClinicService, LanguageService } from 'core';
import { PlaceholderPanelComponent, ScsStateComponent } from 'ui';

/**
 * Everything on the patient site sits inside a clinic (`scs-heta.com/<clinic>`). This shell
 * finds out which clinic the address names. An address that names none shows one "not
 * available" page, the same for every such address; an earlier name of a clinic is moved to
 * its current one, keeping the rest of the address (spec 002, User Story 3).
 */
@Component({
  selector: 'app-clinic-shell',
  imports: [TranslocoPipe, PlaceholderPanelComponent, ScsStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (status().state) {
      @case ('loading') {
        <main class="mx-auto max-w-xl p-6">
          <scs-state state="loading" />
        </main>
      }
      @case ('not-available') {
        <main class="mx-auto max-w-xl p-6" data-testid="clinic-not-available">
          <scs-state state="empty" [message]="'app.clinic.notAvailable' | transloco" />
        </main>
      }
      @case ('failed') {
        <main class="mx-auto max-w-xl p-6" data-testid="clinic-failed">
          <scs-state
            state="error"
            [message]="problem()?.title ?? ''"
            [reference]="problem()?.traceId ?? ''"
            (retry)="load()"
          />
        </main>
      }
      @case ('ready') {
        <p class="mx-auto max-w-xl px-6 pt-6 text-lg font-semibold" data-testid="clinic-name">
          {{ clinicName() }}
        </p>
        <scs-placeholder-panel appNameKey="app.shell.name" />
      }
    }
  `,
})
export class ClinicShellComponent implements OnInit {
  private readonly clinics = inject(ClinicService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService).language;

  protected readonly status = this.clinics.status;

  protected readonly problem = computed(() => {
    const status = this.status();
    return status.state === 'failed' ? status.problem : null;
  });

  protected readonly clinicName = computed(() => {
    const status = this.status();
    if (status.state !== 'ready') {
      return '';
    }
    return this.language() === 'ar' ? status.clinic.nameAr : status.clinic.nameEn;
  });

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    const status = await this.clinics.load();
    if (status.state !== 'ready') {
      return;
    }

    // Keep the address canonical: the current name, in lower case, with the rest untouched.
    const tree = this.router.parseUrl(this.router.url);
    const segments = tree.root.children['primary']?.segments ?? [];
    if (segments.length > 0 && segments[0].path !== status.clinic.shortName) {
      segments[0].path = status.clinic.shortName;
      await this.router.navigateByUrl(tree, { replaceUrl: true });
    }
  }
}
