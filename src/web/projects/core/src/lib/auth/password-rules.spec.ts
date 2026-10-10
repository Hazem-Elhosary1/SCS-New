import { checkPassword, rulesRefused } from './password-rules';

/**
 * The same table runs against the server's rules in
 * src/server/tests/Scs.UnitTests/Identity/IdentityRulesTests.cs — keep the two identical.
 * Columns: password, length, cases, digit, symbol.
 */
const CASES: readonly (readonly [string, boolean, boolean, boolean, boolean])[] = [
  ['', false, false, false, false],
  ['Correct-Horse-9!', true, true, true, true],
  ['Short1!aB', false, true, true, true],
  ['Exactly10!', true, true, true, true],
  ['alllowercase1!', true, false, true, true],
  ['ALLUPPERCASE1!', true, false, true, true],
  ['No-Digits-Here!', true, true, false, true],
  ['NoSymbolHere12', true, true, true, false],
  ['Space only 123', true, true, true, false],
  ['كلمة سرّ Aa1!', true, true, true, true],
  ['كلمةسرطويلةجدا١٢٣', true, false, false, false],
  ['Emoji only 12🙂', true, true, true, false],
  ['Under_score12', true, true, true, true],
  ['Currency12€ab', true, true, true, true],
  ['Ünïcödé-Lettérs1', true, true, true, true],
];

describe('checkPassword', () => {
  for (const [password, length, cases, digit, symbol] of CASES) {
    it(`judges "${password}" as the server does`, () => {
      const check = checkPassword(password);

      expect([check.length, check.cases, check.digit, check.symbol]).toEqual([
        length,
        cases,
        digit,
        symbol,
      ]);
      expect(check.ok).toBe(length && cases && digit && symbol);
    });
  }

  it('refuses a password over two hundred characters', () => {
    const atLimit = checkPassword('Aa1!' + 'x'.repeat(196));
    const over = checkPassword('Aa1!' + 'x'.repeat(197));

    expect([atLimit.tooLong, atLimit.ok]).toEqual([false, true]);
    expect([over.tooLong, over.ok]).toEqual([true, false]);
  });
});

describe('rulesRefused', () => {
  it('maps the field codes of a refusal onto the rule lines, in the order of the lines', () => {
    expect(rulesRefused(['NO_SYMBOL', 'TOO_SHORT'])).toEqual(['length', 'symbol']);
    expect(rulesRefused(['NO_UPPER_LOWER', 'NO_DIGIT'])).toEqual(['cases', 'digit']);
    expect(rulesRefused(['REQUIRED'])).toEqual(['length']);
    expect(rulesRefused(['SAME_AS_CURRENT'])).toEqual([]);
    expect(rulesRefused([])).toEqual([]);
  });
});
