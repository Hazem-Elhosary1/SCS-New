import { ChangeDetectionStrategy, Component, ViewEncapsulation } from '@angular/core';

/**
 * Loads the clinic's own tokens (declared on `[data-scs-look='clinic']`) while the gallery shows the
 * clinic look, so shared components inside `data-scs-look="clinic"` are drawn from the clinic's
 * file, not a copy (spec 095, research R3). Gallery only: never delivered.
 */
@Component({
  selector: 'app-gallery-clinic-look',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../../../../clinic/src/styles/tokens.css', './clinic-reset.css'],
  template: '',
})
export class ClinicLookComponent {}
