import kinds from './file-kinds.json';

export type FileType = 'pdf' | 'png' | 'jpeg';

/** One kind of file, as the platform's table has it (spec 009). */
export interface FileKind {
  readonly key: string;
  readonly types: readonly FileType[];
  readonly maxBytes: number;
  /** How many one record holds; `null` for any number. */
  readonly maxPerRecord: number | null;
  readonly public: boolean;
}

const BY_KEY = new Map<string, FileKind>(
  (kinds as { kinds: FileKind[] }).kinds.map((kind) => [kind.key, kind]),
);

/** The kind, or `undefined` for a key the table does not hold. */
export function fileKind(key: string): FileKind | undefined {
  return BY_KEY.get(key);
}

const KILOBYTE = 1024;
const MEGABYTE = 1024 * 1024;

function number(value: number, language: 'ar' | 'en', decimals: number): string {
  return new Intl.NumberFormat(language === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB', {
    minimumFractionDigits: 0,
    maximumFractionDigits: decimals,
    useGrouping: false,
  }).format(value);
}

/**
 * A size as people read it, in the digits of the language: «٢٫٤ ميجابايت» · "2.4 MB",
 * «٢١٠ كيلوبايت» · "210 KB" (BRD Chapter 08 §3-3).
 */
export function sizeLabel(bytes: number, language: 'ar' | 'en'): string {
  if (bytes < MEGABYTE) {
    const kilobytes = Math.max(1, Math.round(bytes / KILOBYTE));
    return `${number(kilobytes, language, 0)} ${language === 'ar' ? 'كيلوبايت' : 'KB'}`;
  }
  return `${number(bytes / MEGABYTE, language, 1)} ${language === 'ar' ? 'ميجابايت' : 'MB'}`;
}

/**
 * The day a file was uploaded, as the design's card writes it: «٢١ أغسطس ٢٠٢٦» ·
 * "21 Aug 2026". In Cairo time, like every date of the product.
 */
export function fileDateLabel(instant: string, language: 'ar' | 'en'): string {
  const parts = new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG-u-nu-arab' : 'en-US', {
    day: 'numeric',
    month: language === 'ar' ? 'long' : 'short',
    year: 'numeric',
    timeZone: 'Africa/Cairo',
  }).formatToParts(new Date(instant));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((found) => found.type === type)?.value ?? '';
  return `${part('day')} ${part('month')} ${part('year')}`;
}

/** What a kind takes, in words: «PDF أو صورة» · "PDF or image". */
export function acceptedLabel(kind: FileKind, language: 'ar' | 'en'): string {
  const pdf = kind.types.includes('pdf');
  const image = kind.types.includes('png') || kind.types.includes('jpeg');
  if (pdf && image) {
    return language === 'ar' ? 'PDF أو صورة' : 'PDF or image';
  }
  if (image) {
    return language === 'ar' ? 'صورة' : 'Image';
  }
  return 'PDF';
}

/** What the browser's file chooser is told to offer; the platform decides whatever it offers. */
export function acceptAttribute(kind: FileKind): string {
  const extensions: Record<FileType, string> = {
    pdf: '.pdf,application/pdf',
    png: '.png,image/png',
    jpeg: '.jpg,.jpeg,image/jpeg',
  };
  return kind.types.map((type) => extensions[type]).join(',');
}

/** What a file's name says it is — a first, friendly check; the platform reads the content. */
export function typeByName(name: string): FileType | null {
  const extension = name.slice(name.lastIndexOf('.') + 1).toLowerCase();
  if (extension === 'pdf') {
    return 'pdf';
  }
  if (extension === 'png') {
    return 'png';
  }
  return extension === 'jpg' || extension === 'jpeg' ? 'jpeg' : null;
}
