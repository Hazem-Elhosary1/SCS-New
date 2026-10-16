import inventory from './design-inventory.json';

/** The design file's inventory (spec 095, data-model "Design inventory"). */
export interface InventorySet {
  readonly name: string;
  readonly en: string;
  readonly ar: string;
  readonly props: readonly string[];
  /** [values joined by "|", English node, Arabic node] */
  readonly variants: readonly (readonly [string, string, string])[];
}
export interface InventoryPage {
  readonly page: string;
  readonly node: string;
  readonly sets: readonly InventorySet[];
  readonly parts?: Readonly<Record<string, string>>;
  readonly standalone?: Readonly<Record<string, { en: string; ar: string }>>;
}

export const INVENTORY = inventory as unknown as { readonly pages: readonly InventoryPage[] };

/** Lower case, words joined by "-": "Select Trigger" → "select-trigger", "Primary|Small" → "primary-small". */
export const kebab = (text: string): string =>
  text
    .toLowerCase()
    .replace(/&/g, 'and')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

/** What the gallery knows of a component beyond the inventory (data-model "Catalogue entry"). */
export interface CatalogueEntry {
  /** `shared` when the clinic file draws it too (FR-004), with the clinic node it is checked against. */
  readonly kind: 'shared' | 'ops';
  readonly clinicNode?: string;
  /** The existing part of the `ui` library it is (FR-006). */
  readonly replaces?: string;
}

/** Keyed by "<design page>/<set>" (research R1, `contracts/components.md`). */
export const CATALOGUE: Readonly<Record<string, CatalogueEntry>> = {
  'Avatar/Avatar': { kind: 'shared', clinicNode: 'Avatar (4228:7)' },
  'Badge/Badge': { kind: 'shared', clinicNode: '4253:53652', replaces: 'scs-status-mark' },
  'Button/Button': { kind: 'shared', clinicNode: '4252:2227', replaces: 'button[scsButton]' },
  'KPI Card/KPI Card': { kind: 'shared', clinicNode: '4267:1175', replaces: 'scs-kpi-card' },
  'Sidebar Nav Item/Sidebar Nav Item': { kind: 'shared', clinicNode: '4252:60310' },
  'Checkbox/Checkbox': { kind: 'shared', clinicNode: '4252:58206', replaces: 'scs-checkbox' },
  'Radio/Radio': { kind: 'shared', clinicNode: '4252:58465' },
  'Switch/Switch': { kind: 'shared', clinicNode: '4252:58656', replaces: 'scs-switch' },
  'Slider/Slider': { kind: 'shared', clinicNode: '4260:809' },
  'Input/Input': { kind: 'shared', clinicNode: '4252:56552', replaces: 'scs-text-field' },
  'Textarea/Textarea': { kind: 'shared', clinicNode: '4272:885', replaces: 'scs-textarea' },
  'File Upload/File Upload': { kind: 'shared', clinicNode: '4260:938', replaces: 'scs-file-upload' },
  'Dropdown/Select Trigger': { kind: 'shared', clinicNode: '4272:1534', replaces: 'scs-select' },
  'Loading/Spinner': { kind: 'shared', clinicNode: '4252:59832' },
  'Loading/Progress Bar': { kind: 'shared', clinicNode: '4252:59743' },
  'Loading/Skeleton Line': { kind: 'shared', clinicNode: '4252:59877', replaces: 'scs-skeleton' },
  'Alert/Alert': { kind: 'shared', clinicNode: '4253:54128', replaces: 'scs-banner' },
  'Modal/Modal': { kind: 'shared', clinicNode: '4252:61695', replaces: 'scs-dialog' },
  'Navigation/Tab Item': { kind: 'shared', clinicNode: '4252:60720', replaces: 'scs-tabs' },
  'Navigation/Breadcrumb Item': { kind: 'shared', clinicNode: '4252:60901', replaces: 'scs-breadcrumb' },
  'Navigation/Pagination Item': { kind: 'shared', clinicNode: '4252:60984', replaces: 'scs-table-footer' },
  'Navigation/Stepper Step': { kind: 'shared', clinicNode: '4252:61106' },
  'Data Display/Card': { kind: 'shared', clinicNode: '4252:61428', replaces: 'scs-card' },
  'Data Display/Tag': { kind: 'shared', clinicNode: '4253:53827' },
  'Data Display/Accordion Item': { kind: 'shared', clinicNode: '4260:1383' },
  'Data Display/Divider': { kind: 'ops' },
  'Data Display/Stat': { kind: 'ops' },
  'Date Picker/Date Field Trigger': { kind: 'shared', clinicNode: '4256:824' },
  'Table/Table Row': { kind: 'shared', clinicNode: '4267:903' },
  'Time Picker/Time Field Trigger': { kind: 'shared', clinicNode: '4256:1542' },
  'Date & Time Picker/Date & Time Field Trigger': { kind: 'ops' },
  'Media/Image': { kind: 'ops' },
  'Notifications/Notification Bell': { kind: 'shared', clinicNode: '4252:60501' },
  'Notifications/Notification Item': { kind: 'shared', clinicNode: 'Notification Item (4228:7)' },
  'Menu/Menu Item': { kind: 'shared', clinicNode: '4252:61814', replaces: '[scsMenuItem]' },
  'Empty State/Empty State': { kind: 'shared', clinicNode: '4252:60047', replaces: 'scs-empty-state' },
  'Tooltip/Tooltip': { kind: 'shared', clinicNode: '4252:59606', replaces: '[scsTooltip]' },
  'Segmented Control/Segment': { kind: 'shared', clinicNode: '4260:1432' },
  'Usage Meter/Usage Meter': { kind: 'shared', clinicNode: 'Plan Usage Meter (4228:7)' },
  'Date Range Picker/Date Range Trigger': { kind: 'ops' },
  'Chart/Sparkline': { kind: 'ops' },
  'AI/AI Badge': { kind: 'ops' },
  'AI/AI Insight Card': { kind: 'shared', clinicNode: 'Insight Card (4228:7)' },
  'AI/Message Bubble': { kind: 'shared', clinicNode: 'Chat Bubble (4228:7)' },
};

/** The states of the pointer and the keyboard, shown still by the gallery's own attribute (research R5). */
export const STILL_STATES: Readonly<Record<string, 'hover' | 'focus' | 'pressed'>> = {
  Hover: 'hover',
  'Danger Hover': 'hover',
  Focus: 'focus',
  Pressed: 'pressed',
};

export function setOf(page: string, set: string): { page: InventoryPage; set: InventorySet } {
  const inventoryPage = INVENTORY.pages.find((candidate) => candidate.page === page);
  const inventorySet = inventoryPage?.sets.find((candidate) => candidate.name === set);
  if (!inventoryPage || !inventorySet) throw new Error(`Not in the design inventory: ${page} / ${set}`);
  return { page: inventoryPage, set: inventorySet };
}
