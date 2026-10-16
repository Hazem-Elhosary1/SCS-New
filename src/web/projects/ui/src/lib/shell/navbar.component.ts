import { ChangeDetectionStrategy, Component, ElementRef, booleanAttribute, computed, input, output, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ScsAvatarComponent } from '../avatar/avatar.component';

/**
 * The bar across the top of a console (design: Navbar — `278:3` / `311:16`; with an unread count
 * `2403:6546`; with an uploaded logo `2467:133`; the clinic's Navbar `4252:60501`): the console's
 * mark or its uploaded logo and its name, a link home; at the end whatever the page puts there (the
 * bell and its panel), and the person signed in — their name, their role, their initials — as the
 * button of their account menu, which the page draws under it. A start slot holds the button that
 * opens the side bar on a phone. Each look draws it through `--scs-navbar-*`.
 *
 * Its parts are found by `testId`: `<testId>-brand`, `-account`, `-person`, `-role` and
 * `-initials`. On a phone the names give way to the initials; a frame that folds at a wider width
 * (the operators' side bar goes behind a control below 1024) sets `compact` there.
 */
@Component({
  selector: 'scs-navbar',
  imports: [RouterLink, ScsAvatarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.compact]': 'compact()' },
  styles: `
    /* As wide as its container, whatever its words: the name gives way first. */
    :host {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-md);
      box-sizing: border-box;
      contain: inline-size;
      block-size: 64px;
      padding-inline: var(--scs-spacing-lg);
      border-block-end: 1px solid var(--scs-navbar-line, var(--scs-color-border-default));
      background: var(--scs-color-bg-surface);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-inline-size: 0;
      color: var(--scs-color-text-primary);
      font-size: 16px;
      font-weight: var(--scs-navbar-name-weight, 600);
      text-decoration: none;
    }
    .brand:focus-visible,
    .who:focus-visible {
      outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
      outline-offset: 2px;
    }
    .mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 36px;
      block-size: 36px;
      border-radius: 10px;
      background: var(--scs-color-brand-default);
      color: var(--scs-color-brand-on-brand);
      font-size: 18px;
      font-weight: 700;
    }
    .logo {
      display: block;
      block-size: 36px;
      max-inline-size: 160px;
      object-fit: contain;
    }
    .name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .space {
      flex: 1;
    }
    .end {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
    .account {
      position: relative;
    }
    /* The button's padding is its hover's room: a look may give it back to the bar's edge. */
    .who {
      display: flex;
      align-items: center;
      gap: var(--scs-navbar-account-gap, var(--scs-spacing-12));
      margin-inline-end: var(--scs-navbar-account-inset, 0px);
      padding: var(--scs-spacing-xs) var(--scs-spacing-sm);
      border: 0;
      border-radius: var(--scs-radius-md);
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .who:hover,
    .who[aria-expanded='true'] {
      background: var(--scs-color-bg-surface-secondary);
    }
    .names {
      display: flex;
      flex-direction: column;
      max-inline-size: 200px;
    }
    .names span {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .person {
      font-size: 14px;
      font-weight: 600;
    }
    .role {
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
    }
    /* On a phone the names give way to the initials. */
    @media (max-width: 720px) {
      :host {
        padding-inline: var(--scs-spacing-md);
      }
      .names {
        display: none;
      }
    }
    :host(.compact) .names {
      display: none;
    }
  `,
  template: `
    <ng-content select="[scsNavbarStart]" />
    <a class="brand" [routerLink]="home()" [attr.data-testid]="testId() + '-brand'">
      @if (logo()) {
        <img class="logo" [src]="logo()" [alt]="name()" />
      } @else {
        <span class="mark" aria-hidden="true">{{ shownMark() }}</span>
        <span class="name">{{ name() }}</span>
      }
    </a>
    <span class="space"></span>
    <div class="end">
      <ng-content />
      @if (person()) {
        <div class="account">
          <button
            #accountButton
            type="button"
            class="who"
            aria-haspopup="menu"
            [attr.data-testid]="testId() + '-account'"
            [attr.aria-expanded]="accountOpen()"
            [attr.aria-label]="accountLabel() || null"
            (click)="account.emit('pointer')"
            (keydown.arrowdown)="$event.preventDefault(); account.emit('keyboard')"
          >
            <span class="names">
              <span class="person" dir="auto" [attr.title]="person()" [attr.data-testid]="testId() + '-person'">{{ person() }}</span>
              @if (role()) {
                <span class="role" dir="auto" [attr.title]="role()" [attr.data-testid]="testId() + '-role'">{{ role() }}</span>
              }
            </span>
            <scs-avatar size="md" [name]="person()" aria-hidden="true" [attr.data-testid]="testId() + '-initials'" />
          </button>
          <ng-content select="[scsNavbarMenu]" />
        </div>
      }
    </div>
  `,
})
export class ScsNavbarComponent {
  /** The console's name: "SCS — Ops Console" · «SCS — لوحة التشغيل». */
  readonly name = input.required<string>();
  /** The letter in the mark; the name's first letter when not given. */
  readonly mark = input('');
  /** An uploaded logo, in place of the mark and the name (its name is the logo's text). */
  readonly logo = input<string | null>(null);
  readonly home = input('/');
  /** The person signed in, and their role. */
  readonly person = input('');
  readonly role = input('');
  /** Already translated: the account button's name. */
  readonly accountLabel = input('');
  /** Their account menu is open. */
  readonly accountOpen = input(false, { transform: booleanAttribute });
  /** Only the initials, whatever the width (a frame that folds wider than a phone). */
  readonly compact = input(false, { transform: booleanAttribute });
  /** The start of each part's `data-testid`. */
  readonly testId = input('navbar');

  /** The account button was pressed, or Down was pressed on it: the menu then opens on its first item. */
  readonly account = output<'pointer' | 'keyboard'>();

  private readonly accountButton = viewChild<ElementRef<HTMLButtonElement>>('accountButton');

  /** Gives the focus back to the account button, as a menu that closes by Escape does. */
  focusAccount(): void {
    this.accountButton()?.nativeElement.focus();
  }

  protected readonly shownMark = computed(() => this.mark() || [...this.name()][0] || '');
}
