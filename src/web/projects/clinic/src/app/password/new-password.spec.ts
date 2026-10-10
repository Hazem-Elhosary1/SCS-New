import { ScsProblemError } from 'core';
import { NewPasswordForm, bannerFor } from './new-password';

function refusal(code: string, status: number, errors?: Record<string, string[]>, wait?: number) {
  return new ScsProblemError(
    { type: 'urn:scs:error:x', title: code, status, code, traceId: 'a'.repeat(32), errors },
    false,
    wait ?? null,
  );
}

/** Spec 004, FR-027 and FR-028: how a new password stands against the four rules. */
describe('NewPasswordForm', () => {
  it('judges what is typed as it is typed, and refuses nothing before the form is submitted', () => {
    const form = new NewPasswordForm();

    expect(form.met()).toEqual({ length: false, cases: false, digit: false, symbol: false });
    expect(form.submitted()).toBe(false);

    form.type('Abc1');
    expect(form.met()).toEqual({ length: false, cases: true, digit: true, symbol: false });

    form.type('Brand-New-Pass-7!');
    expect(form.met()).toEqual({ length: true, cases: true, digit: true, symbol: true });
  });

  it('may be sent only when every rule is met', () => {
    const form = new NewPasswordForm();

    form.type('short');
    expect(form.submit()).toBe(false);
    expect(form.submitted()).toBe(true);

    form.type('Brand-New-Pass-7!');
    expect(form.submit()).toBe(true);
  });

  it('says a password over two hundred characters is too long, once submitted', () => {
    const form = new NewPasswordForm();
    form.type('Aa1!' + 'x'.repeat(197));

    expect(form.tooLong()).toBe(false);
    expect(form.submit()).toBe(false);
    expect(form.tooLong()).toBe(true);
  });

  it('shows the same-password refusal until the person types again', () => {
    const form = new NewPasswordForm();
    form.type('Correct-Horse-9!');
    form.submit();

    const shown = form.refuse(refusal('VALIDATION_FAILED', 400, { password: ['SAME_AS_CURRENT'] }));

    expect(shown).toBe(true);
    expect(form.sameAsCurrent()).toBe(true);
    expect(form.met()).toEqual({ length: true, cases: true, digit: true, symbol: true });

    form.type('Correct-Horse-9!x');
    expect(form.sameAsCurrent()).toBe(false);
  });

  it('puts a rule the platform refused on its own line, whatever the browser thought', () => {
    const form = new NewPasswordForm();
    form.type('Brand-New-Pass-7!');
    form.submit();

    form.refuse(refusal('VALIDATION_FAILED', 400, { password: ['NO_SYMBOL', 'TOO_SHORT'] }));
    expect(form.met()).toEqual({ length: false, cases: true, digit: true, symbol: false });
    expect(form.sameAsCurrent()).toBe(false);

    // Typing again is a fresh start.
    form.type('Brand-New-Pass-7!!');
    expect(form.met()).toEqual({ length: true, cases: true, digit: true, symbol: true });
  });

  it('leaves a failure that is not about the password to the banner', () => {
    const form = new NewPasswordForm();

    expect(form.refuse(refusal('RATE_LIMITED', 429))).toBe(false);
    expect(form.refuse(refusal('VALIDATION_FAILED', 400, { email: ['REQUIRED'] }))).toBe(false);
    expect(form.refuse(new Error('boom'))).toBe(false);
  });
});

describe('bannerFor', () => {
  it('names the three things a password form can say in a banner', () => {
    expect(bannerFor(refusal('RATE_LIMITED', 429, undefined, 37))).toEqual({
      kind: 'rateLimited',
      seconds: 37,
    });
    expect(bannerFor(refusal('RATE_LIMITED', 429))).toEqual({ kind: 'rateLimited', seconds: 60 });
    expect(bannerFor(refusal('INTERNAL_ERROR', 500))).toEqual({ kind: 'unexpected' });
    expect(bannerFor(new Error('boom'))).toEqual({ kind: 'unexpected' });

    const offline = new ScsProblemError(
      { type: 'x', title: '', status: 0, code: 'SERVICE_UNAVAILABLE', traceId: 'a'.repeat(32) },
      true,
    );
    expect(bannerFor(offline)).toEqual({ kind: 'unreachable' });
  });
});
