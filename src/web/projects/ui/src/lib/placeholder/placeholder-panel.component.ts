import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { PlatformStatusService, RuntimeConfigService } from 'core';

/**
 * The one page each front end ships in spec 001: it shows that the app started and whether it
 * can reach the platform (FR-003). Real screens replace it in later specs.
 *
 * The visible text is still written here in Arabic; User Story 5 of spec 001 moves it to
 * translation keys and adds the language switch and the shared state component.
 */
@Component({
  selector: 'scs-placeholder-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="mx-auto max-w-xl p-6" data-testid="placeholder">
      <h1 class="text-2xl font-semibold">{{ appName() }}</h1>
      <p class="mt-1 text-sm opacity-70" data-testid="environment">{{ environment }}</p>

      <section class="mt-6" aria-live="polite">
        @if (checking()) {
          <p data-testid="platform-checking">جارٍ التحقق من الاتصال بالمنصّة…</p>
        }

        @if (platform(); as platform) {
          <p data-testid="platform-reachable">المنصّة متاحة</p>
          <dl class="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt>الإصدار</dt>
            <dd data-testid="platform-version">
              <bdi>{{ platform.version }}</bdi>
            </dd>
            <dt>البيئة</dt>
            <dd data-testid="platform-environment">
              <bdi>{{ platform.environment }}</bdi>
            </dd>
          </dl>
        }

        @if (problem(); as problem) {
          <div role="alert" data-testid="platform-unreachable">
            <p>{{ problem.title || 'تعذّر الاتصال بالمنصّة' }}</p>
            <p class="mt-1 text-sm">
              المرجع:
              <code data-testid="reference"
                ><bdi>{{ problem.traceId }}</bdi></code
              >
            </p>
            <button type="button" class="mt-3 underline" (click)="check()">إعادة المحاولة</button>
          </div>
        }
      </section>
    </main>
  `,
})
export class PlaceholderPanelComponent implements OnInit {
  private readonly status = inject(PlatformStatusService);

  readonly appName = input.required<string>();
  readonly environment = inject(RuntimeConfigService).value.environment;

  protected readonly checking = computed(() => this.status.status().state === 'checking');

  protected readonly platform = computed(() => {
    const current = this.status.status();
    return current.state === 'reachable' ? current.platform : null;
  });

  protected readonly problem = computed(() => {
    const current = this.status.status();
    return current.state === 'unreachable' ? current.problem : null;
  });

  ngOnInit(): void {
    this.check();
  }

  protected check(): void {
    void this.status.check();
  }
}
