import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PlanBody, PlansService } from './plans.service';

const body: PlanBody = {
  nameAr: 'الباقة الأساسية',
  nameEn: 'Basic Plan',
  description: 'للعيادات الصغيرة.',
  monthlyEgp: 249,
  annualEgp: 0,
  monthlyUsd: 12,
  annualUsd: 0,
  badge: null,
  note: null,
  features: [{ key: 'max_users', limit: 3 }],
};

describe('the plans’ addresses (contracts/plans-api.md)', () => {
  let service: PlansService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PlansService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the list, one plan, and the two grids', async () => {
    const list = service.list();
    http.expectOne({ method: 'GET', url: '/v1/ops/plans' }).flush({ items: [] });
    expect(await list).toEqual({ items: [] });

    const one = service.read('a b');
    http.expectOne({ method: 'GET', url: '/v1/ops/plans/a%20b' }).flush({ id: 'a b' });
    expect((await one).id).toBe('a b');

    const add = service.grid();
    http.expectOne({ method: 'GET', url: '/v1/ops/plans/grid' }).flush({ items: [], badgeHolder: null });
    await add;

    const edit = service.grid('p1');
    http.expectOne({ method: 'GET', url: '/v1/ops/plans/p1/grid' }).flush({ items: [], badgeHolder: null });
    await edit;
  });

  it('adds with the body and answers the id and the code', async () => {
    const added = service.add(body);
    const request = http.expectOne({ method: 'POST', url: '/v1/ops/plans' });
    expect(request.request.body).toEqual(body);
    request.flush({ id: 'p1', code: 'basic-plan' }, { status: 201, statusText: 'Created' });
    expect(await added).toEqual({ id: 'p1', code: 'basic-plan' });
  });

  it('edits with the version, disables with a reason, and enables with nothing', async () => {
    const edited = service.edit('p1', { ...body, version: 3 });
    const put = http.expectOne({ method: 'PUT', url: '/v1/ops/plans/p1' });
    expect(put.request.body.version).toBe(3);
    put.flush(null, { status: 204, statusText: 'No Content' });
    await edited;

    const disabled = service.disable('p1', 'قديمة');
    const off = http.expectOne({ method: 'POST', url: '/v1/ops/plans/p1/disable' });
    expect(off.request.body).toEqual({ reason: 'قديمة' });
    off.flush(null, { status: 204, statusText: 'No Content' });
    await disabled;

    const enabled = service.enable('p1');
    const on = http.expectOne({ method: 'POST', url: '/v1/ops/plans/p1/enable' });
    expect(on.request.body).toBeNull();
    on.flush(null, { status: 204, statusText: 'No Content' });
    await enabled;
  });
});
