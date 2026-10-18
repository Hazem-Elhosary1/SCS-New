import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService, ScsProblemError } from 'core';
import { of } from 'rxjs';
import { ToastService } from 'ui';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { ClinicDetailPage } from './clinic-detail.page';
import { ClinicDetail, ClinicSettings, ClinicsService, OperatorRef } from './clinics.service';

const settle = () => new Promise((resolve) => setTimeout(resolve));
const words = ar.app.clinics.detail;

function detail(extra: Partial<ClinicDetail> = {}): ClinicDetail {
  return {
    id: 'c1',
    slug: 'noor',
    nameAr: 'عيادة النور',
    nameEn: 'Al Noor Clinic',
    createdAt: '2026-10-01T09:00:00Z',
    createdBy: { id: 'o1', name: 'سارة' },
    version: 'v1',
    status: { code: 'trial', ar: 'تجربة', en: 'Trial', tone: 'info' },
    plan: { id: 'plan-basic', nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan' },
    billingCycle: null,
    nextRenewal: null,
    address: 'القاهرة',
    taxNumber: null,
    specialty: 'dental',
    defaultLanguage: 'ar',
    signInAddress: 'https://app.scs-heta.com',
    owner: {
      name: 'أحمد علي',
      email: 'owner@example.com',
      mobile: '+201001234567',
      state: 'invited',
      lastSignInAt: null,
      invitation: { state: 'live', at: '2026-10-08T09:00:00Z' },
    },
    accountManager: null,
    managers: [{ id: 'o2', name: 'منى' }],
    doctors: 0,
    branches: 1,
    can: { edit: true },
    ...extra,
  };
}

const problem = (code: string, status = 409) =>
  new ScsProblemError({ type: 'x', title: code, status, code, traceId: 'a'.repeat(32) }, false);

class FakeClinics {
  reads = 0;
  readonly assigned: (string | null)[] = [];
  answer: () => Promise<ClinicDetail> = async () => detail();
  read = () => {
    this.reads++;
    return this.answer();
  };
  assign: (id: string, version: string, operatorId: string | null) => Promise<{ accountManager: OperatorRef | null; version: string }> =
    async (_id, _version, operatorId) => {
      this.assigned.push(operatorId);
      return { accountManager: operatorId === null ? null : { id: operatorId, name: 'منى' }, version: 'v2' };
    };
  invite: (id: string) => Promise<{ expiresAt: string }> = async () => ({ expiresAt: '2026-10-24T09:00:00Z' });
  settings: (id: string) => Promise<ClinicSettings> = async () => ({
    defaultLanguage: 'ar',
    timeZone: 'Africa/Cairo',
    staffAddress: 'https://app.scs-heta.com',
    can: { edit: true },
  });
}

class FakeToasts {
  readonly said: { text: string; tone?: string }[] = [];
  show(text: string, ...rest: [string?, string?]) {
    this.said.push({ text, tone: rest[1] });
  }
}

async function draw(fake: FakeClinics, codes = [OpsCan.clinicsView, OpsCan.clinicsEdit]) {
  const spoken = signal<'ar' | 'en'>('ar');
  const held = new Set<string>(codes);
  const toasts = new FakeToasts();
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
      { provide: ToastService, useValue: toasts },
      { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'c1' })), snapshot: { paramMap: convertToParamMap({ id: 'c1' }) } } },
    ],
  });
  const fixture = TestBed.createComponent(ClinicDetailPage);
  fixture.detectChanges();
  await settle();
  fixture.detectChanges();
  return { fixture, toasts };
}

const element = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const has = (fixture: ComponentFixture<unknown>, testId: string) => element(fixture).querySelector(`[data-testid="${testId}"]`) !== null;
const said = (fixture: ComponentFixture<unknown>, testId: string) =>
  element(fixture).querySelector(`[data-testid="${testId}"]`)?.textContent?.trim() ?? '';
const click = async (fixture: ComponentFixture<unknown>, testId: string) => {
  element(fixture).querySelector<HTMLButtonElement>(`[data-testid="${testId}"]`)!.click();
  await settle();
  fixture.detectChanges();
};

describe('the clinic’s page (spec 015, User Story 3)', () => {
  it('heads the page with the Latin mark, the name and the status, and shows the owner', async () => {
    const { fixture } = await draw(new FakeClinics());
    expect(said(fixture, 'clinic-detail-mark')).toBe('AN');
    expect(said(fixture, 'clinic-detail-name')).toContain('عيادة النور');
    expect(said(fixture, 'clinic-detail-status')).toBe('تجربة');
    expect(said(fixture, 'clinic-info-email')).toContain('owner@example.com');
    expect(said(fixture, 'clinic-info-team')).toContain('0 أطباء');
    expect(has(fixture, 'clinic-invitation')).toBe(true);
  });

  it('offers editing, assigning and sending the invitation again only to an editor', async () => {
    const editor = await draw(new FakeClinics());
    expect(has(editor.fixture, 'clinic-edit')).toBe(true);
    expect(has(editor.fixture, 'clinic-manager-assign')).toBe(true);
    expect(has(editor.fixture, 'clinic-invite-resend')).toBe(true);

    TestBed.resetTestingModule();
    const fake = new FakeClinics();
    fake.answer = async () => detail({ can: { edit: false }, managers: [] });
    const viewer = await draw(fake, [OpsCan.clinicsView]);
    expect(has(viewer.fixture, 'clinic-edit')).toBe(false);
    expect(has(viewer.fixture, 'clinic-manager-assign')).toBe(false);
    expect(has(viewer.fixture, 'clinic-invite-resend')).toBe(false);
  });

  it('assigns the account manager with the clinic’s version and says it was saved', async () => {
    const fake = new FakeClinics();
    const { fixture, toasts } = await draw(fake);
    await click(fixture, 'clinic-manager-assign');
    (fixture.componentInstance as unknown as { chosenManager: { set: (v: string) => void } }).chosenManager.set('o2');
    await click(fixture, 'clinic-manager-save');
    expect(fake.assigned).toEqual(['o2']);
    expect(said(fixture, 'clinic-manager-name')).toContain('منى');
    expect(toasts.said.at(-1)?.text).toBe(words.managerSaved);
  });

  it('disables «تعديل العيادة» while offline, as every write', async () => {
    const { fixture } = await draw(new FakeClinics());
    const edit = () => element(fixture).querySelector<HTMLButtonElement>('[data-testid="clinic-edit"]')!;
    expect(edit().disabled).toBe(false);
    globalThis.dispatchEvent(new Event('offline'));
    fixture.detectChanges();
    expect(edit().disabled).toBe(true);
    globalThis.dispatchEvent(new Event('online'));
    fixture.detectChanges();
    expect(edit().disabled).toBe(false);
  });

  it('reads the clinic again when an assignment finds it changed elsewhere', async () => {
    const fake = new FakeClinics();
    fake.assign = async () => {
      throw problem('CLINIC_CHANGED_ELSEWHERE');
    };
    const { fixture, toasts } = await draw(fake);
    const before = fake.reads;
    await click(fixture, 'clinic-manager-assign');
    await click(fixture, 'clinic-manager-save');
    expect(toasts.said.at(-1)).toEqual({ text: words.changedElsewhere, tone: 'danger' });
    expect(fake.reads).toBe(before + 1);
    expect(has(fixture, 'clinic-manager-select')).toBe(false);
  });

  it('does not preselect a manager who is no longer offered', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => detail({ accountManager: { id: 'gone', name: 'سابق' }, managers: [{ id: 'o2', name: 'منى' }] });
    const { fixture } = await draw(fake);
    await click(fixture, 'clinic-manager-assign');
    expect((fixture.componentInstance as unknown as { chosenManager: () => string }).chosenManager()).toBe('');
  });

  it('sends the invitation again and reads the clinic anew; a cooldown is said and nothing reread', async () => {
    const fake = new FakeClinics();
    const { fixture, toasts } = await draw(fake);
    const before = fake.reads;
    await click(fixture, 'clinic-invite-resend');
    expect(fake.reads).toBe(before + 1);
    expect(toasts.said.at(-1)?.tone).toBeUndefined();

    fake.invite = async () => {
      throw problem('RESET_EMAIL_COOLDOWN', 429);
    };
    await click(fixture, 'clinic-invite-resend');
    expect(toasts.said.at(-1)).toEqual({ text: words.invitation.cooldown, tone: 'danger' });
    expect(fake.reads).toBe(before + 1);
  });

  it('answers a clinic that is not there as one that could not be read, with a retry', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => {
      throw problem('NOT_FOUND', 404);
    };
    const { fixture } = await draw(fake);
    expect(has(fixture, 'clinic-error')).toBe(true);
    expect(has(fixture, 'clinic-retry')).toBe(true);
  });
});
