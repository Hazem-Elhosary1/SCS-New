import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { RuntimeConfigService } from 'core';
import { App } from './app';

async function appIn(environment: string): Promise<HTMLElement> {
  await TestBed.configureTestingModule({
    imports: [App, TranslocoTestingModule.forRoot({ langs: { ar: {} } })],
    providers: [
      provideRouter([]),
      { provide: RuntimeConfigService, useValue: { value: { environment } } },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(App);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('App', () => {
  it.each(['local', 'test'])('says the data is not real in the %s environment', async (environment) => {
    const root = await appIn(environment);

    expect(root.querySelector('[data-testid="env-strip"]')).not.toBeNull();
    expect(root.style.getPropertyValue('--scs-env-strip')).toBe('32px');
  });

  it('shows no strip in production', async () => {
    const root = await appIn('production');

    expect(root.querySelector('[data-testid="env-strip"]')).toBeNull();
    expect(root.style.getPropertyValue('--scs-env-strip')).toBe('0px');
  });
});
