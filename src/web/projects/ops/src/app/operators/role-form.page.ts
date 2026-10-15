import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsBannerComponent,
  ScsBreadcrumbComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsCheckboxComponent,
  ScsEmptyStateComponent,
  ScsLinkDirective,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
  ScsStatusMarkComponent,
  ScsTextFieldComponent,
  ScsTextareaComponent,
  ToastService,
} from 'ui';
import { OPS_PERMISSIONS, OpsAccess, inCatalogueOrder } from '../access/ops-access.service';
import { opsNumber } from '../i18n/ops-format';
import { CatalogueGroup, OperatorsService, RoleRow, RoleView, TeamCodes, codeOf, fieldErrors } from './operators.service';

const NEEDS = new Map(OPS_PERMISSIONS.map((permission) => [permission.code, permission.needs]));

/** The codes given and every code they need: what a save keeps (data-model §1). */
export function withNeeded(codes: Iterable<string>): Set<string> {
  const kept = new Set(codes);
  for (const code of [...kept]) {
    const needed = NEEDS.get(code);
    if (needed) {
      kept.add(needed);
    }
  }
  return kept;
}

/** The ticked permissions that need this one: while any is ticked, it cannot be unticked. */
export function neededBy(code: string, ticked: ReadonlySet<string>): string[] {
  return OPS_PERMISSIONS.filter((permission) => permission.needs === code && ticked.has(permission.code)).map(
    (permission) => permission.code,
  );
}

/**
 * «إضافة دور جديد» and «تعديل الدور» (spec 011, FR-012, FR-014 to FR-018; frames
 * `1744:676`/`1749:744`, `1751:813`/`1752:884`): a name, a description, and the catalogue's 28
 * boxes in their nine groups. A permission that another ticked one needs is ticked and locked,
 * with the reason; one the maker does not hold cannot be ticked, with the reason. The platform
 * checks all of it again.
 */
@Component({
  selector: 'app-role-form-page',
  imports: [
    TranslocoPipe,
    ScsBreadcrumbComponent,
    ScsCardComponent,
    ScsTextFieldComponent,
    ScsSelectComponent,
    ScsTextareaComponent,
    ScsCheckboxComponent,
    ScsStatusMarkComponent,
    ScsButtonDirective,
    ScsLinkDirective,
    ScsBannerComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      /* The frames draw a form's labels small, grey and regular. */
      --scs-field-label-weight: 400;
      --scs-field-label-color: var(--scs-color-text-secondary);
      --scs-select-empty-color: var(--scs-color-text-primary);
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-title-sm);
      font-weight: 500;
      overflow-wrap: anywhere;
    }
    .note {
      margin: var(--scs-spacing-2) 0 var(--scs-spacing-md);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    scs-banner {
      margin-block-end: var(--scs-spacing-md);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--scs-spacing-md) var(--scs-spacing-lg);
      margin-block-end: var(--scs-spacing-md);
    }
    .wide {
      grid-column: 1 / -1;
    }
    .category .label {
      display: block;
      margin-block-end: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .category .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-md);
    }
    .category .box {
      display: inline-flex;
      align-items: center;
      min-block-size: 46px;
      padding-inline: var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
    }
    .category .hint {
      flex: 1;
      min-inline-size: 160px;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .permissions {
      margin-block-start: var(--scs-spacing-md);
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-subtle);
    }
    .permissions > header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
    }
    h2 {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 600;
    }
    .all {
      min-block-size: 40px;
      padding-inline: var(--scs-spacing-sm);
      border: 0;
      background: none;
      font: inherit;
      font-size: var(--scs-text-body-sm);
      cursor: pointer;
    }
    .error {
      margin: var(--scs-spacing-xs) 0 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-feedback-danger-text);
    }
    fieldset {
      margin: var(--scs-spacing-md) 0 0;
      padding: 0;
      border: 0;
    }
    legend {
      padding: 0;
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
    }
    .boxes {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      column-gap: var(--scs-spacing-lg);
    }
    .row {
      margin-block-start: var(--scs-spacing-md);
    }
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-subtle);
    }
    .buttons button {
      min-block-size: 40px;
      padding-block: 0;
      padding-inline: var(--scs-spacing-lg);
      font-size: var(--scs-text-body);
    }
    @media (max-width: 720px) {
      .grid,
      .boxes {
        grid-template-columns: minmax(0, 1fr);
      }
      .buttons {
        flex-direction: column-reverse;
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.operators.crumbs' | transloco"
      [crumbs]="[
        { label: 'app.operators.title' | transloco, link: '/operators' },
        { label: 'app.operators.roles.title' | transloco, link: '/operators/roles' },
        { label: (editing ? 'app.operators.roleForm.editCrumb' : 'app.operators.roleForm.addCrumb') | transloco },
      ]"
    />
    <h1 data-testid="role-form-title">
      @if (editing) {
        {{ 'app.operators.roleForm.editTitle' | transloco: { name: originalName() } }}
      } @else {
        {{ 'app.operators.roleForm.addTitle' | transloco }}
      }
    </h1>
    <p class="note">{{ (editing ? 'app.operators.roleForm.editNote' : 'app.operators.roleForm.addNote') | transloco }}</p>

    @switch (state()) {
      @case ('loading') {
        <scs-card><scs-skeleton data-testid="role-form-loading" [rows]="8" [label]="'app.operators.roleForm.loading' | transloco" /></scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state context="error" data-testid="role-form-failed" [title]="'app.operators.roleForm.failed' | transloco">
            <button type="button" scsButton="secondary" data-testid="role-form-retry" (click)="load()">
              {{ 'app.operators.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (readOnly(); as reason) {
          <scs-banner tone="info" data-testid="role-read-only">{{
            (reason === 'system' ? 'app.operators.roleForm.systemRole' : 'app.operators.roleForm.ownRole') | transloco
          }}</scs-banner>
        }
        @if (changed()) {
          <scs-banner tone="warning" data-testid="role-changed" [actionLabel]="'app.operators.form.reload' | transloco" (action)="load()">{{
            'app.operators.form.changed' | transloco
          }}</scs-banner>
        }
        @if (failure(); as words) {
          <scs-banner tone="danger" data-testid="role-problem">{{ words }}</scs-banner>
        }

        <scs-card>
          <form novalidate (submit)="$event.preventDefault(); save()">
            <div class="grid">
              <scs-text-field
                testId="role-name"
                [label]="'app.operators.roleForm.name' | transloco"
                [placeholder]="'app.operators.roleForm.namePlaceholder' | transloco"
                [error]="said('name')"
                [value]="name()"
                (valueChange)="readOnly() || name.set($event)"
              />
              @if (!editing) {
                <scs-select
                  testId="role-based-on"
                  [label]="'app.operators.roleForm.basedOn' | transloco"
                  [placeholder]="'app.operators.roleForm.basedOnNone' | transloco"
                  [options]="sources()"
                  [hint]="leftWords()"
                  [value]="basedOn()"
                  (valueChange)="base($event)"
                />
              } @else {
                <div class="category" data-testid="role-category">
                  <span class="label" id="role-category-label">{{ 'app.operators.roleForm.category' | transloco }}</span>
                  <div class="row">
                    <span class="box" aria-labelledby="role-category-label">
                      <scs-status-mark tone="brand">{{
                        (systemKey() ? 'app.operators.roles.system' : 'app.operators.roles.custom') | transloco
                      }}</scs-status-mark>
                    </span>
                    <span class="hint" data-testid="role-category-hint">{{
                      (systemKey() ? 'app.operators.roleForm.categorySystem' : 'app.operators.roleForm.categoryCustom') | transloco
                    }}</span>
                  </div>
                </div>
              }
              <scs-textarea
                class="wide"
                testId="role-description"
                [label]="'app.operators.roleForm.description' | transloco"
                [placeholder]="'app.operators.roleForm.descriptionPlaceholder' | transloco"
                [max]="500"
                [readOnly]="readOnly() !== null"
                [error]="said('description')"
                [(value)]="description"
              />
            </div>

            <section class="permissions" data-testid="role-permissions">
              <header>
                <h2>{{ 'app.operators.roleForm.permissions' | transloco }}</h2>
                @if (readOnly() === null) {
                  <button type="button" scsLink class="all" data-testid="select-all" (click)="toggleAll()">
                    {{ (allTicked() ? 'app.operators.roleForm.clearAll' : 'app.operators.roleForm.selectAll') | transloco }}
                  </button>
                }
              </header>
              @if (said('codes'); as words) {
                <p class="error" role="alert" data-testid="role-codes-error">{{ words }}</p>
              }

              @for (group of groups(); track group.key) {
                <fieldset [attr.data-testid]="'group-' + group.key">
                  <legend>{{ access.nameOfGroup(group.key) }}</legend>
                  <div class="boxes">
                    @for (box of group.items; track box.code) {
                      <scs-checkbox
                        [testId]="'box-' + box.code"
                        [label]="access.nameOfPermission(box.code)"
                        [checked]="ticked().has(box.code)"
                        (checkedChange)="tick(box.code, $event)"
                        [disabled]="lockReason(box) !== ''"
                        [reason]="lockReason(box) === 'read-only' ? '' : lockReason(box)"
                      />
                    }
                  </div>
                </fieldset>
              }
            </section>

            @if (editing && readOnly() === null) {
              <div class="row">
                <scs-textarea
                  testId="role-note"
                  [label]="'app.operators.roleForm.note' | transloco"
                  [hint]="'app.operators.form.noteHint' | transloco"
                  [max]="500"
                  [rows]="2"
                  [error]="said('note')"
                  [(value)]="note"
                />
              </div>
            }

            <div class="buttons">
              <button type="button" scsButton="secondary" data-testid="role-cancel" (click)="leave()">
                {{ 'app.operators.cancel' | transloco }}
              </button>
              @if (readOnly() === null) {
                <button type="submit" scsButton data-testid="role-save" [busy]="saving()" [disabled]="saving()">
                  {{
                    (saving()
                      ? 'app.operators.roleForm.saving'
                      : editing
                        ? 'app.operators.roleForm.saveChanges'
                        : 'app.operators.roleForm.save'
                    ) | transloco
                  }}
                </button>
              }
            </div>
          </form>
        </scs-card>
      }
    }
  `,
})
export class RoleFormPage {
  private readonly team = inject(OperatorsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly access = inject(OpsAccess);

  private readonly id = this.route.snapshot.paramMap.get('id');
  protected readonly editing = this.id !== null;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly groups = signal<readonly CatalogueGroup[]>([]);
  private readonly original = signal<RoleView | null>(null);
  private readonly others = signal<readonly RoleRow[]>([]);

  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly note = signal('');
  protected readonly basedOn = signal('');
  protected readonly ticked = signal<ReadonlySet<string>>(new Set());
  private readonly left = signal(0);

  protected readonly saving = signal(false);
  protected readonly changed = signal(false);
  protected readonly failure = signal('');
  private readonly errors = signal<Record<string, string>>({});

  protected readonly readOnly = computed(() => this.original()?.readOnlyReason ?? null);
  protected readonly systemKey = computed(() => this.original()?.systemKey ?? null);
  protected readonly originalName = computed(() => {
    const role = this.original();
    return role === null ? '' : this.access.nameOfRole(role);
  });

  /** What this person may tick: every box the platform marked, in the catalogue's order. */
  private readonly grantable = computed(
    () => new Set(this.groups().flatMap((group) => group.items.filter((box) => box.mayGrant).map((box) => box.code))),
  );

  /** What the role held when the form opened: it stays even where this person does not hold it (ق-63). */
  private readonly held = computed(() => new Set(this.original()?.codes ?? []));

  protected readonly allTicked = computed(() => {
    const can = this.grantable();
    return can.size > 0 && [...can].every((code) => this.ticked().has(code));
  });

  protected readonly sources = computed<ScsSelectOption[]>(() =>
    this.others().map((role) => ({ value: role.id, label: this.access.nameOfRole(role) })),
  );

  protected readonly leftWords = computed(() =>
    this.left() === 0
      ? ''
      : this.words.translate('app.operators.roleForm.left', { count: opsNumber(this.left(), this.language.language()) }),
  );

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.changed.set(false);
    this.failure.set('');
    this.errors.set({});
    try {
      const [groups, role, list] = await Promise.all([
        this.team.catalogue(),
        this.id === null ? Promise.resolve(null) : this.team.role(this.id),
        this.id === null ? this.team.roles() : Promise.resolve(null),
      ]);
      this.groups.set(groups);
      this.original.set(role);
      this.others.set(list?.items ?? []);
      if (role !== null) {
        this.name.set(role.name);
        this.description.set(role.description ?? '');
        this.ticked.set(new Set(role.codes));
        this.note.set('');
      }
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }

  protected said(field: string): string {
    return this.errors()[field] ?? '';
  }

  /** Why a box cannot be changed; empty when it can. */
  protected lockReason(box: { code: string; mayGrant: boolean }): string {
    if (this.readOnly() !== null) {
      return 'read-only';
    }
    const ticked = this.ticked();
    const needing = neededBy(box.code, ticked);
    if (ticked.has(box.code) && needing.length > 0) {
      return this.words.translate('app.operators.roleForm.neededBy', { permission: this.access.nameOfPermission(needing[0]) });
    }
    // A box the role already had may be unticked by anyone who edits it; one it did not have
    // may be ticked only by whoever holds it.
    return box.mayGrant || this.held().has(box.code) ? '' : this.words.translate('app.operators.roleForm.notYours');
  }

  protected tick(code: string, on: boolean): void {
    const next = new Set(this.ticked());
    if (on) {
      next.add(code);
    } else {
      next.delete(code);
    }
    this.ticked.set(withNeeded(next));
    this.clear('codes');
  }

  /** «حدد الكل» ticks what this person may tick, and nothing more; pressed again, it clears them. */
  protected toggleAll(): void {
    const can = this.grantable();
    const next = new Set(this.ticked());
    const clearing = this.allTicked();
    for (const code of can) {
      if (clearing) {
        next.delete(code);
      } else {
        next.add(code);
      }
    }
    this.ticked.set(clearing ? withNeeded(next) : next);
    this.clear('codes');
  }

  /** Starts from another role: what this person holds of it, and how much was left out (FR-017). */
  protected async base(roleId: string): Promise<void> {
    this.basedOn.set(roleId);
    if (roleId === '') {
      this.left.set(0);
      return;
    }
    try {
      const source = await this.team.role(roleId);
      this.ticked.set(new Set(source.copy.codes));
      this.left.set(source.copy.left.length);
    } catch {
      this.basedOn.set('');
      this.left.set(0);
    }
  }

  protected leave(): void {
    void this.router.navigateByUrl('/operators/roles');
  }

  protected async save(): Promise<void> {
    const errors: Record<string, string> = {};
    const name = this.name().trim();
    if (name === '') {
      errors['name'] = this.words.translate('app.operators.required');
    } else if (name.length > 100) {
      errors['name'] = this.words.translate('app.operators.tooLong');
    }
    if (this.description().trim().length > 500) {
      errors['description'] = this.words.translate('app.operators.tooLong');
    }
    if (this.ticked().size === 0) {
      errors['codes'] = this.words.translate('app.operators.roleForm.noneTicked');
    }
    this.errors.set(errors);
    this.failure.set('');
    if (Object.keys(errors).length > 0) {
      this.focusFirstError();
      return;
    }

    this.saving.set(true);
    const body = {
      name,
      description: this.description().trim() || null,
      codes: inCatalogueOrder([...this.ticked()]),
    };
    try {
      if (this.id === null) {
        await this.team.createRole({ ...body, basedOnId: this.basedOn() || null });
        this.toasts.show(this.words.translate('app.operators.roleForm.created'));
      } else {
        await this.team.editRole(this.id, { ...body, note: this.note().trim() || null, version: this.original()?.version });
        this.toasts.show(this.words.translate('app.operators.roleForm.updated'));
      }
      await this.router.navigateByUrl('/operators/roles');
    } catch (error) {
      this.refused(error);
    } finally {
      this.saving.set(false);
    }
  }

  private refused(error: unknown): void {
    const code = codeOf(error);
    if (code === TeamCodes.roleNameTaken) {
      this.errors.set({ name: this.words.translate('app.operators.roleForm.nameTaken') });
      this.focusFirstError();
    } else if (code === TeamCodes.permissionsNotHeld) {
      const codes = (error as { problem?: { permissions?: string[] } }).problem?.permissions ?? [];
      this.failure.set(
        this.words.translate('app.operators.roleForm.notHeld', {
          permissions: codes.map((refusedCode) => this.access.nameOfPermission(refusedCode)).join(this.language.language() === 'ar' ? '، ' : ', '),
        }),
      );
    } else if (code === TeamCodes.changedElsewhere) {
      this.changed.set(true);
    } else if (code === TeamCodes.ownRoleEdit || code === TeamCodes.systemRole) {
      void this.load();
    } else if (code === TeamCodes.validation) {
      const named = fieldErrors(error);
      this.errors.set(
        Object.fromEntries(
          Object.entries(named).map(([field, reason]) => [
            field,
            this.words.translate(
              field === 'codes' ? 'app.operators.roleForm.noneTicked' : reason === 'TOO_LONG' ? 'app.operators.tooLong' : 'app.operators.required',
            ),
          ]),
        ),
      );
      this.focusFirstError();
    } else {
      this.failure.set(this.words.translate(this.editing ? 'app.operators.roleForm.saveFailed' : 'app.operators.roleForm.createFailed'));
    }
  }

  private clear(field: string): void {
    if (this.errors()[field] !== undefined) {
      this.errors.set(Object.fromEntries(Object.entries(this.errors()).filter(([name]) => name !== field)));
    }
  }

  private focusFirstError(): void {
    setTimeout(() =>
      this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"], [data-testid="select-all"]')?.focus(),
    );
  }
}
