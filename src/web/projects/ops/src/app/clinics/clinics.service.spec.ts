import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ClinicsService, EditClinicBody, NewClinicBody } from './clinics.service';

const added: NewClinicBody = {
  nameAr: 'عيادة النور',
  nameEn: 'Al Noor Clinic',
  ownerName: 'أحمد علي',
  email: 'owner@example.com',
  mobile: '+201001234567',
  address: 'القاهرة',
  planId: null,
  taxNumber: '',
  specialty: null,
};

const edited: EditClinicBody = {
  version: 'AAAAAAAAB9E=',
  nameAr: 'عيادة النور',
  nameEn: 'Al Noor Clinic',
  ownerName: 'أحمد علي',
  email: 'owner@example.com',
  mobile: '',
  address: '',
  taxNumberSent: false,
  specialty: 'dental',
  defaultLanguage: 'ar',
  note: '',
};

describe('the clinics’ addresses (contracts/ops-clinics-api.md)', () => {
  let service: ClinicsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ClinicsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for the list with nothing when nothing is chosen, the newest first being the default', async () => {
    const list = service.list({ search: '  ', sort: 'new' });
    const request = http.expectOne((r) => r.method === 'GET' && r.url === '/v1/ops/clinics');
    expect(request.request.params.keys()).toEqual([]);
    request.flush({ items: [] });
    expect(await list).toEqual({ items: [] });
  });

  it('carries every choice of the list, each status once', async () => {
    const list = service.list({
      search: ' نور ',
      statuses: ['trial', 'active'],
      plan: 'plan-basic',
      length: 'trial',
      endsWithin: 7,
      sort: 'ends-asc',
      page: 2,
      size: 25,
    });
    const request = http.expectOne((r) => r.url === '/v1/ops/clinics');
    const params = request.request.params;
    expect(params.get('search')).toBe('نور');
    expect(params.getAll('status')).toEqual(['trial', 'active']);
    expect(params.get('plan')).toBe('plan-basic');
    expect(params.get('length')).toBe('trial');
    expect(params.get('endsWithin')).toBe('7');
    expect(params.get('sort')).toBe('ends-asc');
    expect(params.get('page')).toBe('2');
    expect(params.get('size')).toBe('25');
    request.flush({ items: [] });
    await list;
  });

  it('reads the form’s choices, adds, reads and edits one clinic', async () => {
    const form = service.form();
    http.expectOne({ method: 'GET', url: '/v1/ops/clinics/form' }).flush({ plans: [], specialties: [] });
    expect((await form).plans).toEqual([]);

    const add = service.add(added);
    const posted = http.expectOne({ method: 'POST', url: '/v1/ops/clinics' });
    expect(posted.request.body).toEqual(added);
    posted.flush({ id: 'c1', slug: 'noor' });
    expect(await add).toEqual({ id: 'c1', slug: 'noor' });

    const one = service.read('a b');
    http.expectOne({ method: 'GET', url: '/v1/ops/clinics/a%20b' }).flush({ id: 'a b' });
    expect((await one).id).toBe('a b');

    const edit = service.edit('c1', edited);
    const put = http.expectOne({ method: 'PUT', url: '/v1/ops/clinics/c1' });
    expect(put.request.body).toEqual(edited);
    put.flush(null, { status: 204, statusText: 'No Content' });
    await edit;
  });

  it('assigns the account manager, sends the invitation again and reads the settings', async () => {
    const assign = service.assign('c1', 'v1', null);
    const put = http.expectOne({ method: 'PUT', url: '/v1/ops/clinics/c1/account-manager' });
    expect(put.request.body).toEqual({ version: 'v1', operatorId: null });
    put.flush({ accountManager: null, version: 'v2' });
    expect((await assign).version).toBe('v2');

    const invite = service.invite('c1');
    const posted = http.expectOne({ method: 'POST', url: '/v1/ops/clinics/c1/invitation' });
    expect(posted.request.body).toBeNull();
    posted.flush({ expiresAt: '2026-10-24T09:00:00Z' });
    expect((await invite).expiresAt).toBe('2026-10-24T09:00:00Z');

    const settings = service.settings('c1');
    http
      .expectOne({ method: 'GET', url: '/v1/ops/clinics/c1/settings' })
      .flush({ defaultLanguage: 'ar', timeZone: 'Africa/Cairo', staffAddress: 'https://app.scs-heta.com', can: { edit: false } });
    expect((await settings).timeZone).toBe('Africa/Cairo');
  });
});
