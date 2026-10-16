import { ActivityLine } from './operators.service';

/** The kinds of activity the console has a sentence for (contracts/operator-screens.md §2). */
export const ACTIVITY_KINDS = [
  'signed-in',
  'operator.invited',
  'operator.edited',
  'operator.role-changed',
  'operator.suspended',
  'operator.reactivated',
  'operator.link-sent',
  'operator.unlocked',
  'opsrole.created',
  'opsrole.edited',
  'opsrole.deleted',
  // Spec 012: the feature catalogue.
  'feature.created',
  'feature.updated',
  'feature.retired',
  'feature.resumed',
] as const;

/**
 * One line of an operator's recent activity as a sentence: «دعا نور خالد بدور مشاهد فقط».
 * The platform sends the kind and the names; the words are the page's. A kind the console
 * does not know yet — one a later spec adds — still reads as something, never as a code.
 *
 * @param translate the page's words, by key and values.
 * @param roleName a role's name in the page's language: a system role by its key, a custom one as written.
 */
export function activitySentence(
  line: ActivityLine,
  translate: (key: string, values: Record<string, string>) => string,
  roleName: (role: { name: string; systemKey: string | null }) => string,
): string {
  const known = (ACTIVITY_KINDS as readonly string[]).includes(line.kind);
  return translate(`app.operators.activity.${known ? line.kind : 'other'}`, {
    name: line.name ?? '',
    device: line.device ?? '',
    role: roleName({ name: line.role ?? '', systemKey: line.roleKey }),
  });
}
