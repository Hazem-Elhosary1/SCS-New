import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CONFIG_FETCH, RuntimeConfigService, apiInterceptor } from 'core';
import { PlaceholderPanelComponent } from './placeholder-panel.component';

const VERSION = '/api/v1/meta/version';
const READY = '/api/health/ready';

const release = {
  version: '0.1.0+3f9c2ab',
  commit: '3f9c2ab4d5e6f708192a3b4c5d6e7f8091a2b3c4',
  builtAt: '2026-10-10T09:30:00Z',
  environment: 'test',
};

describe('PlaceholderPanelComponent', () => {
  let fixture: ComponentFixture<PlaceholderPanelComponent>;
  let backend: HttpTestingController;

  const text = (testId: string): string | undefined =>
    (fixture.nativeElement as HTMLElement)
      .querySelector(`[data-testid="${testId}"]`)
      ?.textContent?.trim();

  const settle = async (): Promise<void> => {
    // Let the status check finish: its two answers are joined before the state changes.
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [PlaceholderPanelComponent],
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        {
          provide: CONFIG_FETCH,
          useValue: async () => ({ environment: 'test', apiBaseUrl: '/api' }),
        },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    backend = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(PlaceholderPanelComponent);
    fixture.componentRef.setInput('appName', 'لوحة العيادة');
    fixture.detectChanges();
  });

  afterEach(() => backend.verify());

  it('shows the app name, the environment and that it is checking', () => {
    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'لوحة العيادة',
    );
    expect(text('environment')).toBe('test');
    expect(text('platform-checking')).toBeTruthy();

    backend.expectOne(VERSION).flush(release);
    backend.expectOne(READY).flush({ status: 'Healthy' });
  });

  it('shows the platform as reachable with its version and environment', async () => {
    backend.expectOne(VERSION).flush(release);
    backend.expectOne(READY).flush({ status: 'Healthy' });
    await settle();

    expect(text('platform-reachable')).toBeTruthy();
    expect(text('platform-version')).toBe('0.1.0+3f9c2ab');
    expect(text('platform-environment')).toBe('test');
    expect(text('platform-unreachable')).toBeUndefined();
  });

  it('shows an error state with a reference when the platform is not ready, and can retry', async () => {
    backend.expectOne(VERSION).flush(release);
    backend
      .expectOne(READY)
      .flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });
    await settle();

    expect(text('platform-unreachable')).toBeTruthy();
    expect(text('reference')).toMatch(/^[0-9a-f]{32}$/);
    expect(text('platform-reachable')).toBeUndefined();

    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    backend.expectOne(VERSION).flush(release);
    backend.expectOne(READY).flush({ status: 'Healthy' });
    await settle();

    expect(text('platform-reachable')).toBeTruthy();
  });
});
