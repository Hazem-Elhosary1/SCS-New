import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { filter, map, startWith } from 'rxjs';
import { ScsButtonDirective, ScsIconComponent, ScsPageHeaderComponent, ScsTab, ScsTabsComponent } from 'ui';
import { OpsCan } from '../access/ops-access.service';
import { OpsCanDirective } from '../access/ops-can';
import { OperatorsListComponent } from './operators-list.component';
import { RolesListComponent } from './roles-list.component';

/**
 * «المشغلون» (spec 011, User Stories 3 and 6; frames `1615:71`, `1735:557` and their English
 * twins): one page with two tabs — the team, and its roles. Each tab is an address of its own,
 * so a link or a reload lands on the tab it names.
 */
@Component({
  selector: 'app-operators-page',
  imports: [
    TranslocoPipe,
    ScsPageHeaderComponent,
    ScsTabsComponent,
    ScsButtonDirective,
    ScsIconComponent,
    OpsCanDirective,
    OperatorsListComponent,
    RolesListComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    scs-tabs {
      display: flex;
      margin-block: var(--scs-spacing-sm) var(--scs-spacing-lg);
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    button[scsButton] {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
    }
  `,
  template: `
    @if (tab() === 'roles') {
      <scs-page-header [title]="'app.operators.roles.title' | transloco" [note]="'app.operators.roles.note' | transloco">
        <button *appCan="can.operatorsManageRoles" type="button" scsButton data-testid="add-role" (click)="go('/operators/roles/new')">
          <scs-icon name="plus" [size]="16" />
          <span>{{ 'app.operators.roles.add' | transloco }}</span>
        </button>
      </scs-page-header>
    } @else {
      <scs-page-header [title]="'app.operators.title' | transloco" [note]="'app.operators.note' | transloco">
        <button *appCan="can.operatorsManage" type="button" scsButton data-testid="add-operator" (click)="go('/operators/new')">
          <scs-icon name="plus" [size]="16" />
          <span>{{ 'app.operators.add' | transloco }}</span>
        </button>
      </scs-page-header>
    }

    <scs-tabs
      role="tablist"
      [attr.aria-label]="'app.operators.tabs.label' | transloco"
      [tabs]="tabs()"
      [active]="tab()"
      (activeChange)="open($event)"
    />

    @if (tab() === 'roles') {
      <app-roles-list />
    } @else {
      <app-operators-list />
    }
  `,
})
export class OperatorsPage {
  private readonly router = inject(Router);

  protected readonly can = OpsCan;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  protected readonly tab = computed(() => (this.url().split('?')[0].endsWith('/roles') ? 'roles' : 'operators'));

  private readonly words = inject(TranslocoService);
  private readonly spoken = toSignal(this.words.selectTranslation(), { initialValue: null });

  // Written again when the language changes: the labels are the page's words.
  protected readonly tabs = computed<readonly ScsTab[]>(() => {
    this.spoken();
    return [
      { key: 'operators', label: this.words.translate('app.operators.tabs.operators') },
      { key: 'roles', label: this.words.translate('app.operators.tabs.roles') },
    ];
  });

  protected go(path: string): void {
    void this.router.navigateByUrl(path);
  }

  protected open(tab: string): void {
    void this.router.navigateByUrl(tab === 'roles' ? '/operators/roles' : '/operators');
  }
}
