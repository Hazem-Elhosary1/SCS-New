import { Injectable } from '@angular/core';

/**
 * Carries one thing from the code step back to the sign-in form: a refusal that must be shown
 * there (the account was disabled, or the clinic suspended, between the two steps). Held in
 * memory and handed over once.
 */
@Injectable({ providedIn: 'root' })
export class SignInFlow {
  private refusal: unknown = null;

  carry(refusal: unknown): void {
    this.refusal = refusal;
  }

  takeRefusal(): unknown {
    const refusal = this.refusal;
    this.refusal = null;
    return refusal;
  }
}
