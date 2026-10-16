/**
 * The gallery's own words, in both languages. They are data here and not keys of the console's
 * translation files, which are delivered with production (spec 095, `contracts/gallery.md`).
 */
export const GALLERY_WORDS = {
  ar: {
    title: 'مكوّنات التصميم',
    note: 'صفحة للتطوير فقط: كل مكوّن من ملف التصميم بكل حالاته، ولا تُسلَّم مع الإنتاج.',
    language: 'اللغة',
    look: 'المظهر',
    operators: 'المشغّلين',
    clinic: 'العيادة',
    sections: 'الأقسام',
    all: 'كل الأقسام',
    shared: 'مشترك',
    own: 'خاص بلوحة المشغّلين',
    notBuilt: 'لم يُبنَ بعد',
    live: 'مثال يعمل',
    onlyShared: 'في مظهر العيادة تظهر المكوّنات المشتركة وحدها.',
  },
  en: {
    title: 'Design components',
    note: 'A page for development only: every component of the design file in every state; not delivered with production.',
    language: 'Language',
    look: 'Look',
    operators: 'Operators',
    clinic: 'Clinic',
    sections: 'Sections',
    all: 'All sections',
    shared: 'Shared',
    own: "The operators console's own",
    notBuilt: 'Not built yet',
    live: 'Working example',
    onlyShared: 'In the clinic look only the shared components are shown.',
  },
} as const;

export type GalleryWord = keyof (typeof GALLERY_WORDS)['en'];
