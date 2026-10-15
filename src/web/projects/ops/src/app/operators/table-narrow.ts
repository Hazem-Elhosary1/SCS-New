/**
 * A table of the team at a phone's width: six columns do not fit, and a table that scrolls
 * sideways hides what it holds. Each row becomes a block — the name and its menu on the first
 * line, then each other cell on a line of its own with its column's name before it. The
 * header row stays for a screen reader and is not drawn.
 */
export const TABLE_NARROW = `
  @media (max-width: 720px) {
    table.scs-table,
    table.scs-table tbody {
      display: block;
    }
    table.scs-table thead {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }
    table.scs-table tr {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: var(--scs-spacing-xs) var(--scs-spacing-sm);
      padding-block: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle);
    }
    /* The row whose menu is open is marked as one block, not cell by cell. */
    table.scs-table tr[data-marked='true'] {
      background: var(--scs-color-bg-surface-secondary);
    }
    table.scs-table tr[data-marked='true'] td {
      background: none;
    }
    table.scs-table tr:last-child {
      border-block-end: 0;
    }
    table.scs-table td {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-sm);
      grid-column: 1 / -1;
      min-inline-size: 0;
      padding: 0;
      border: 0;
      font-size: var(--scs-text-body-sm);
    }
    table.scs-table td:first-child {
      grid-column: 1;
      display: block;
      font-size: var(--scs-text-body);
    }
    table.scs-table td[data-kind='menu'] {
      grid-column: 2;
      grid-row: 1;
      inline-size: auto;
      justify-content: flex-end;
    }
    table.scs-table .scs-row-menu {
      inline-size: 40px;
      block-size: 40px;
    }
    table.scs-table a {
      display: inline-flex;
      align-items: center;
      min-block-size: 40px;
      min-inline-size: 40px;
    }
    table.scs-table td[data-label]::before {
      content: attr(data-label);
      flex: none;
      min-inline-size: 110px;
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-label);
    }
  }
`;
