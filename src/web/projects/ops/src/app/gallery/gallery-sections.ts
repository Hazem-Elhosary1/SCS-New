import { Type } from '@angular/core';

/** One section of the gallery: a page of the design file, "Other parts", a pattern, or the icons. */
export interface GallerySection {
  /** The address part: `/dev/components/<id>`. */
  readonly id: string;
  /** The design page's name, as the design writes it. */
  readonly title: string;
  /** The design page's node in the operators' file, when it has one. */
  readonly node: string | null;
  readonly kind: 'component' | 'other' | 'pattern' | 'icons';
  /** The section's content, once built. */
  readonly load?: () => Promise<Type<unknown>>;
}

const page = (id: string, title: string, node: string, load?: GallerySection['load']): GallerySection => ({
  id,
  title,
  node,
  kind: 'component',
  load,
});

/** In the design file's order (spec 095, `contracts/gallery.md`). */
export const GALLERY_SECTIONS: readonly GallerySection[] = [
  page('avatar', 'Avatar', '8:35', () => import('./sections/avatar.section').then((file) => file.AvatarSection)),
  page('badge', 'Badge', '8:36', () => import('./sections/badge.section').then((file) => file.BadgeSection)),
  page('button', 'Button', '8:37', () => import('./sections/button.section').then((file) => file.ButtonSection)),
  page('kpi-card', 'KPI Card', '8:39', () => import('./sections/kpi-card.section').then((file) => file.KpiCardSection)),
  page('sidebar-nav-item', 'Sidebar Nav Item', '8:40', () => import('./sections/sidebar-nav-item.section').then((file) => file.SidebarNavItemSection)),
  page('checkbox', 'Checkbox', '35:2', () => import('./sections/checkbox.section').then((file) => file.CheckboxSection)),
  page('radio', 'Radio', '35:3', () => import('./sections/choices.sections').then((file) => file.RadioSection)),
  page('switch', 'Switch', '35:4', () => import('./sections/switch.section').then((file) => file.SwitchSection)),
  page('slider', 'Slider', '35:5', () => import('./sections/choices.sections').then((file) => file.SliderSection)),
  page('input', 'Input', '41:2', () => import('./sections/input.section').then((file) => file.InputSection)),
  page('textarea', 'Textarea', '41:3', () => import('./sections/textarea.section').then((file) => file.TextareaSection)),
  page('file-upload', 'File Upload', '41:4', () => import('./sections/file-upload.section').then((file) => file.FileUploadSection)),
  page('dropdown', 'Dropdown', '57:2', () => import('./sections/dropdown.section').then((file) => file.DropdownSection)),
  page('loading', 'Loading', '59:2', () => import('./sections/loading.section').then((file) => file.LoadingSection)),
  page('alert', 'Alert', '60:2', () => import('./sections/alert.section').then((file) => file.AlertSection)),
  page('modal', 'Modal', '62:2', () => import('./sections/modal.section').then((file) => file.ModalSection)),
  page('navigation', 'Navigation', '65:2', () => import('./sections/navigation.section').then((file) => file.NavigationSection)),
  page('data-display', 'Data Display', '68:2', () => import('./sections/data-display.section').then((file) => file.DataDisplaySection)),
  page('date-picker', 'Date Picker', '74:2', () => import('./sections/date-picker.section').then((file) => file.DatePickerSection)),
  page('table', 'Table', '273:2', () => import('./sections/table.section').then((file) => file.TableSection)),
  page('time-picker', 'Time Picker', '77:2', () => import('./sections/time-picker.section').then((file) => file.TimePickerSection)),
  page('date-and-time-picker', 'Date & Time Picker', '79:2', () => import('./sections/date-time-picker.section').then((file) => file.DateTimePickerSection)),
  page('navbar', 'Navbar', '278:2', () => import('./sections/navbar.section').then((file) => file.NavbarSection)),
  page('media', 'Media', '82:2', () => import('./sections/media.section').then((file) => file.MediaSection)),
  page('notifications', 'Notifications', '362:2', () => import('./sections/notifications.section').then((file) => file.NotificationsSection)),
  page('menu', 'Menu', '368:2', () => import('./sections/menu.section').then((file) => file.MenuSection)),
  page('empty-state', 'Empty State', '374:2', () => import('./sections/empty-state.section').then((file) => file.EmptyStateSection)),
  page('tooltip', 'Tooltip', '383:2', () => import('./sections/tooltip.section').then((file) => file.TooltipSection)),
  page('segmented-control', 'Segmented Control', '385:2', () => import('./sections/choices.sections').then((file) => file.SegmentedSection)),
  page('usage-meter', 'Usage Meter', '387:2', () => import('./sections/usage-meter.section').then((file) => file.UsageMeterSection)),
  page('date-range-picker', 'Date Range Picker', '392:2', () => import('./sections/date-range-picker.section').then((file) => file.DateRangePickerSection)),
  page('chart', 'Chart', '394:2', () => import('./sections/chart.section').then((file) => file.ChartSection)),
  page('ai', 'AI', '399:2', () => import('./sections/ai.section').then((file) => file.AiSection)),
  { id: 'other-parts', title: 'Other parts', node: null, kind: 'other', load: () => import('./sections/other-parts.section').then((file) => file.OtherPartsSection) },
  { id: 'pattern-search-filter', title: 'Pattern — Search & Filter', node: '111:2', kind: 'pattern', load: () => import('./sections/pattern-search-filter.section').then((file) => file.PatternSearchFilterSection) },
  { id: 'pattern-page-states', title: 'Pattern — Page States', node: '117:2', kind: 'pattern', load: () => import('./sections/pattern-page-states.section').then((file) => file.PatternPageStatesSection) },
  { id: 'pattern-confirmation', title: 'Pattern — Confirmation', node: '120:2', kind: 'pattern', load: () => import('./sections/pattern-confirmation.section').then((file) => file.PatternConfirmationSection) },
  { id: 'pattern-multi-step-form', title: 'Pattern — Multi-step Form', node: '123:2', kind: 'pattern', load: () => import('./sections/pattern-multi-step-form.section').then((file) => file.PatternMultiStepFormSection) },
  { id: 'pattern-onboarding', title: 'Pattern — Onboarding', node: '253:2', kind: 'pattern', load: () => import('./sections/pattern-onboarding.section').then((file) => file.PatternOnboardingSection) },
  { id: 'icons', title: 'Icons', node: '738:7380', kind: 'icons', load: () => import('./sections/icons.section').then((file) => file.IconsSection) },
];
