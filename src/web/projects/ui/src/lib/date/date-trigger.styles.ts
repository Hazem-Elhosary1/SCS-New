/**
 * The trigger of the date and time fields (Date Field Trigger 74:53, Time Field Trigger 77:53, Date &
 * Time 79:71, Date Range 392:53): a label, a button that names the chosen value or asks for one, its
 * marks at the end, and the message under it. Drawn as the text field, from the same `--scs-field-*`
 * settings each look sets (spec 095); the defaults are the clinic's. The console's own fields (date
 * range, date and time) use it too.
 */
export const SCS_DATE_TRIGGER_STYLES = `
:host {
  display: block;
}
.name {
  display: block;
  margin-block-end: var(--scs-spacing-sm);
  font-size: var(--scs-field-label-size, var(--scs-text-body-sm));
  font-weight: var(--scs-field-label-weight, 500);
  color: var(--scs-field-label-color, var(--scs-color-text-primary));
}
.required {
  color: var(--scs-color-feedback-danger-icon);
}
:host(.off) .name {
  color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
}
.trigger {
  display: flex;
  align-items: center;
  gap: var(--scs-spacing-sm);
  box-sizing: border-box;
  inline-size: 100%;
  block-size: var(--scs-field-height, 44px);
  padding-inline: var(--scs-field-pad, 14px) var(--scs-field-pad, 12px);
  border: 1px solid var(--scs-field-border, var(--scs-color-border-default));
  border-radius: var(--scs-field-radius, var(--scs-radius-md));
  background: var(--scs-color-bg-surface);
  box-shadow: var(--scs-field-shadow, none);
  color: var(--scs-color-text-primary);
  font: inherit;
  font-size: var(--scs-field-text, var(--scs-text-body));
  text-align: start;
  cursor: pointer;
}
.trigger:hover:not(:disabled),
:host-context([data-scs-state~='hover']) .trigger {
  border-color: var(--scs-field-hover-border, var(--scs-color-icon-default));
}
/* Focus and open: a purple border and a 2 ring outside it (State=Focus). */
.trigger:focus-visible,
.trigger[aria-expanded='true'],
:host-context([data-scs-state~='focus']) .trigger {
  border-color: var(--scs-color-brand-default);
  border-width: var(--scs-field-focus-border, 1px);
  outline: 2px solid var(--scs-field-focus-color, var(--scs-color-brand-default));
  outline-offset: var(--scs-field-focus-offset, 1px);
}
.trigger[aria-invalid='true'] {
  border-color: var(--scs-color-feedback-danger-icon);
  border-width: var(--scs-field-state-border, 1px);
}
.trigger:disabled {
  border-color: var(--scs-field-border, var(--scs-color-border-default));
  background: var(--scs-color-bg-muted, var(--scs-color-bg-surface-secondary));
  box-shadow: none;
  cursor: not-allowed;
}
.value {
  flex: 1;
  min-inline-size: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.value.empty {
  color: var(--scs-field-placeholder, var(--scs-color-text-placeholder));
}
.trigger:disabled .value {
  color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
}
.mark {
  flex: none;
  color: var(--scs-color-icon-default);
}
.trigger:disabled .mark {
  color: var(--scs-field-disabled-text, var(--scs-color-text-tertiary));
}
.hint,
.error {
  margin: var(--scs-spacing-xs) 0 0;
  font-size: var(--scs-field-message-size, var(--scs-text-label));
  color: var(--scs-color-text-secondary);
}
.error {
  color: var(--scs-field-error-color, var(--scs-color-feedback-danger-text));
}
/* The open panel, drawn in place for the gallery's still parts. */
.inline {
  margin-block-start: 4px;
}
`;
