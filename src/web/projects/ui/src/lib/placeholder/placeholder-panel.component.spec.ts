import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DOCUMENT } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  CONFIG_FETCH,
  LanguageService,
  RuntimeConfigService,
  VERSION_FETCH,
  VersionCheckService,
  apiInterceptor,
} from 'core';
import { uiTranslations } from '../../testing/ui-testing';
import { PlaceholderPanelComponent } from './placeholder-panel.component';

const VERSION = '/api/v1/meta/version';
const READY = '/api/health/ready';

const release = {
  version: '0.1.0+3f9c2ab',
  commit: '3f9c2ab4d5e6f708192a3b4c5d6e7f8091a2b3c4',
  // 10:00 UTC in July is 13:00 in Cairo (daylight saving time).
  builtAt: '2026-07-01T10:00:00Z',
  environment: 'test',
};

describe('PlaceholderPanelComponent', () => {
  let fixture: ComponentFixture<PlaceholderPanelComponent>;
  let backend: HttpTestingController;
  let element: HTMLElement;

  const text = (testId: string): string | undefined =>
    element.querySelector(`[data-testid="${testId}"]`)?.textContent?.trim();

  const settle = async (): Promise<void> => {
    // Let the status check finish: its two answers are joined before the state changes.
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  async function create(environment: 'test' | 'production' = 'test'): Promise<void> {
    TestBed.configureTestingModule({
      imports: [PlaceholderPanelComponent, uiTranslations()],
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        { provide: CONFIG_FETCH, useValue: async () => ({ environment, apiBaseUrl: '/api' }) },
        {
          provide: VERSION_FETCH,
          useValue: async () => ({
            app: 'clinic',
            version: '0.2.0+abcdef0',
            commit: 'abcdef0'.padEnd(40, '0'),
            builtAt: '2026-10-10T09:30:00Z',
          }),
        },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    TestBed.inject(LanguageService).use('ar');
    backend = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(PlaceholderPanelComponent);
    fixture.componentRef.setInput('appNameKey', 'app.shell.name');
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
  }

  const answerReady = (): void => {
    backend.expectOne(VERSION).flush(release);
    backend.expectOne(READY).flush({ status: 'Healthy' });
  };

  afterEach(() => backend.verify());

  it('shows the app name, the environment and the loading state while it checks', async () => {
    await create();

    expect(text('app-name')).toBe('لوحة العيادة');
    expect(text('environment')).toBe('test');
    expect(element.querySelector('scs-state')?.getAttribute('data-state')).toBe('loading');

    answerReady();
  });

  it('shows the platform as reachable with its version and environment', async () => {
    await create();
    answerReady();
    await settle();

    expect(text('platform-reachable')).toBe('المنصّة متاحة');
    expect(text('platform-version')).toBe('0.1.0+3f9c2ab');
    expect(text('platform-environment')).toBe('test');
    expect(element.querySelector('[data-testid="platform-unreachable"]')).toBeNull();
  });

  it('shows the error state with a reference when the platform is not ready, and can retry', async () => {
    await create();
    backend.expectOne(VERSION).flush(release);
    backend
      .expectOne(READY)
      .flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });
    await settle();

    const error = element.querySelector('[data-testid="platform-unreachable"]');
    expect(error?.getAttribute('data-state')).toBe('error');
    expect(text('state-reference')).toMatch(/^[0-9a-f]{32}$/);
    expect(text('platform-reachable')).toBeUndefined();

    element.querySelector<HTMLButtonElement>('[data-testid="state-retry"]')!.click();
    answerReady();
    await settle();

    expect(text('platform-reachable')).toBeTruthy();
  });

  it('shows the platform title and reference when the platform answers with a failure', async () => {
    await create();
    backend.expectOne(VERSION).flush(
      {
        type: 'urn:scs:error:internal-error',
        title: 'حدث خطأ غير متوقع',
        status: 500,
        code: 'INTERNAL_ERROR',
        traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
      },
      { status: 500, statusText: 'Server Error' },
    );
    backend.expectOne(READY).flush({ status: 'Healthy' });
    await settle();

    expect(text('state-message')).toBe('حدث خطأ غير متوقع');
    expect(text('state-reference')).toBe('4bf92f3577b34da6a3ce929d0e0e4736');
  });

  it('switches language and direction without reload', async () => {
    await create();
    answerReady();
    await settle();
    const root = TestBed.inject(DOCUMENT).documentElement;
    expect(root.dir).toBe('rtl');

    element.querySelector<HTMLButtonElement>('[data-testid="language-switch"]')!.click();
    await settle();

    expect(root.lang).toBe('en');
    expect(root.dir).toBe('ltr');
    expect(text('app-name')).toBe('Clinic console');
    expect(text('platform-reachable')).toBe('Platform is available');

    element.querySelector<HTMLButtonElement>('[data-testid="language-switch"]')!.click();
    await settle();
    expect(root.dir).toBe('rtl');
    expect(text('platform-reachable')).toBe('المنصّة متاحة');
  });

  it('shows a date in Cairo time and an amount in Egyptian pounds', async () => {
    await create();
    answerReady();
    await settle();

    // Arabic: Arabic-Indic digits, 1:00 pm Cairo time.
    expect(text('sample-date')).toContain('١:٠٠');
    expect(text('sample-amount')).toContain('١٬٢٥٠٫٥٠');
    expect(text('sample-amount')).toContain('ج.م');

    element.querySelector<HTMLButtonElement>('[data-testid="language-switch"]')!.click();
    await settle();

    expect(text('sample-date')).toContain('13:00');
    expect(text('sample-amount')).toContain('1,250.50');
    expect(text('sample-amount')).toContain('EGP');
  });

  it('can force each of the six states outside production', async () => {
    await create();
    answerReady();
    await settle();

    for (const state of ['loading', 'empty', 'error', 'offline', 'view-only', 'not-in-plan']) {
      element.querySelector<HTMLButtonElement>(`[data-testid="force-${state}"]`)!.click();
      fixture.detectChanges();
      expect(
        element.querySelector('[data-testid="forced-state"]')?.getAttribute('data-state'),
      ).toBe(state);
    }

    element.querySelector<HTMLButtonElement>('[data-testid="force-none"]')!.click();
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="forced-state"]')).toBeNull();
  });

  it('shows the front end’s own version once version.json has been read', async () => {
    await create();
    answerReady();
    await settle();
    expect(element.querySelector('[data-testid="app-version"]')).toBeNull();

    await TestBed.inject(VersionCheckService).check();
    await settle();

    expect(text('app-version')).toBe('0.2.0+abcdef0');
  });

  it('never shows the state demonstration in production', async () => {
    await create('production');
    answerReady();
    await settle();

    expect(element.querySelector('[data-testid="state-demo"]')).toBeNull();
    expect(text('platform-reachable')).toBeTruthy();
  });
});
