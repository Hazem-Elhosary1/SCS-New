/** The four rules every password must meet (BRD B-R16), in the order the screens list them. */
export const PASSWORD_RULES = ['length', 'cases', 'digit', 'symbol'] as const;

export type PasswordRule = (typeof PASSWORD_RULES)[number];

export const PASSWORD_MIN_LENGTH = 10;
export const PASSWORD_MAX_LENGTH = 200;

export interface PasswordCheck {
  /** At least ten characters. */
  readonly length: boolean;
  /** An upper-case and a lower-case letter. */
  readonly cases: boolean;
  /** A digit, 0 to 9. */
  readonly digit: boolean;
  /** A punctuation mark or a symbol. A space is neither. */
  readonly symbol: boolean;
  /** Longer than the platform accepts. */
  readonly tooLong: boolean;
  /** All four rules are met and it is not too long. */
  readonly ok: boolean;
}

const UPPER = /\p{Lu}/u;
const LOWER = /\p{Ll}/u;
const DIGIT = /[0-9]/;
const SYMBOL = /[\p{P}\p{S}]/u;

/**
 * Says which rules a password meets, as it is typed (spec 004, FR-027). The server is the
 * guard; this is here so nobody has to submit to find out. It judges exactly as the server
 * does — one UTF-16 unit at a time — so the two can never disagree: the same table of cases
 * runs against both (password-rules.spec.ts and the server's IdentityRulesTests).
 */
export function checkPassword(text: string): PasswordCheck {
  const units = text.split('');
  const length = units.length >= PASSWORD_MIN_LENGTH;
  const cases = units.some((unit) => UPPER.test(unit)) && units.some((unit) => LOWER.test(unit));
  const digit = units.some((unit) => DIGIT.test(unit));
  const symbol = units.some((unit) => SYMBOL.test(unit));
  const tooLong = units.length > PASSWORD_MAX_LENGTH;

  return {
    length,
    cases,
    digit,
    symbol,
    tooLong,
    ok: length && cases && digit && symbol && !tooLong,
  };
}

const RULE_OF_CODE: Readonly<Record<string, PasswordRule>> = {
  TOO_SHORT: 'length',
  REQUIRED: 'length',
  NO_UPPER_LOWER: 'cases',
  NO_DIGIT: 'digit',
  NO_SYMBOL: 'symbol',
};

export const PasswordCodes = {
  sameAsCurrent: 'SAME_AS_CURRENT',
  tooLong: 'TOO_LONG',
} as const;

/** The rules the server said were not met, from the field codes of its refusal. */
export function rulesRefused(codes: readonly string[]): PasswordRule[] {
  return PASSWORD_RULES.filter((rule) => codes.some((code) => RULE_OF_CODE[code] === rule));
}
