import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { ClinicSettingsTab } from './clinic-settings.tab';
import { ClinicSettings, ClinicsService } from './clinics.service';

const settle = () => new Promise((resolve) => setTimeout(resolve));
const words = ar.app.clinics.settings;

class FakeClinics {
  answer: () => Promise<ClinicSettings> = async () => ({
    defaultLanguage: 'en',
    timeZone: 'Africa/Cairo',
    staffAddress: 'https://app.scs-heta.com',
    can: { edit: true },
  });
  settings = () => this.answer();
}

async function draw(fake: FakeClinics, codes: string[]) {
  const spoken = signal<'ar' | 'en'>('ar');
  const held = new Set(codes);
  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: { ar, en },
        translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([{ path: '**', children: [] }]),
      { provide: LanguageService, useValue: { language: spoken, use: (l: 'ar' | 'en') => spoken.set(l) } },
      { provide: OpsAccess, useValue: { can: (code: string) => held.has(code) } },
      { provide: ClinicsService, useValue: fake },
    ],
  });
  const fixture = TestBed.createComponent(ClinicSettingsTab);
  fixture.componentRef.setInput('clinicId', 'c1');
  fixture.detectChanges();
  await settle();
  fixture.detectChanges();
  return fixture;
}

const said = (fixture: ComponentFixture<unknown>, testId: string) =>
  (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`)?.textContent?.trim() ?? '';
const has = (fixture: ComponentFixture<unknown>, testId: string) =>
  (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`) !== null;

describe('the clinic’s settings tab (spec 015, User Story 5)', () => {
  it('mirrors the general settings and tells an editor where to change them', async () => {
    const fixture = await draw(new FakeClinics(), [OpsCan.clinicsView, OpsCan.clinicsEdit]);
    expect(said(fixture, 'settings-strip')).toBe(words.stripEditor);
    expect(said(fixture, 'settings-language')).toBe(ar.app.clinics.form.languages.en);
    expect(said(fixture, 'settings-time-zone')).toBe(words.cairo);
    expect(said(fixture, 'settings-staff-address')).toBe('https://app.scs-heta.com');
    expect(has(fixture, 'settings-edit')).toBe(true);
  });

  it('gives a viewer the viewer’s words and no link', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => ({ ...(await new FakeClinics().answer()), can: { edit: false } });
    const fixture = await draw(fake, [OpsCan.clinicsView]);
    expect(said(fixture, 'settings-strip')).toBe(words.stripViewer);
    expect(has(fixture, 'settings-edit')).toBe(false);
  });

  it('offers to try again when the settings cannot be read', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => {
      throw new Error('down');
    };
    const fixture = await draw(fake, [OpsCan.clinicsView]);
    expect(has(fixture, 'settings-error')).toBe(true);
    expect(has(fixture, 'settings-retry')).toBe(true);
  });
});
