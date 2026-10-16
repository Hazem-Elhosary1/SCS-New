import { TranslocoService } from '@jsverse/transloco';
import { OpsCountForms, opsCount } from '../i18n/ops-format';

/**
 * The count of a thing in the page's language, with the forms the i18n files give under
 * `key`: Arabic in its grammatical form, English as one and many.
 */
export function countWords(words: TranslocoService, key: string, value: number, language: 'ar' | 'en'): string {
  const form = (name: string) => words.translate(`${key}.${name}`);
  const forms: OpsCountForms = {
    zero: form('zero'),
    one: form('one'),
    two: form('two'),
    few: form('few'),
    many: form('many'),
    other: form('other'),
  };
  return opsCount(value, forms, language, [form('one'), form('two')]);
}
