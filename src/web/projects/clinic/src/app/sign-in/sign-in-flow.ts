import { Injectable } from '@angular/core';

/**
 * Carries one thing to the sign-in form from the page before it: a refusal met at the code step
 * (the account was disabled, or the clinic suspended, between the two steps), or the news that
 * a password was just set from a link. Held in memory — never in the address — and handed
 * over once.
 */
/** Good news to show on the sign-in form after a password was set from a link (spec 004). */
export interface SignInNotice {
  readonly kind: 'passwordChanged' | 'accountActivated';
  /** The account the link belonged to, to save typing it again. */
  readonly email: string;
}

@Injectable({ providedIn: 'root' })
export class SignInFlow {
  private refusal: unknown = null;
  private notice: SignInNotice | null = null;

  announce(notice: SignInNotice): void {
    this.notice = notice;
  }

  takeNotice(): SignInNotice | null {
    const notice = this.notice;
    this.notice = null;
    return notice;
  }

  carry(refusal: unknown): void {
    this.refusal = refusal;
  }

  takeRefusal(): unknown {
    const refusal = this.refusal;
    this.refusal = null;
    return refusal;
  }
}
