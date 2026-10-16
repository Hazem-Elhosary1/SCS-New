import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsFileCardComponent, ScsFileUploadComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/**
 * The File Upload page (41:4): the zone (default, hover, a file held over it) and, under it, the
 * card of a file uploading, uploaded or refused; a working zone that "uploads" what is dropped.
 */
@Component({
  selector: 'app-gallery-file-upload-section',
  imports: [ScsFileUploadComponent, ScsFileCardComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    /* Room for the part at its widest (320), so a failed upload says why on one line. */
    :host {
      --gallery-cell: min(340px, 100%);
    }
    .unit {
      display: grid;
      gap: var(--scs-spacing-sm);
      inline-size: 100%;
      max-inline-size: 320px;
    }
    .name {
      font-size: var(--scs-text-body-sm);
      font-weight: var(--scs-field-label-weight, 600);
    }
    scs-file-upload,
    scs-file-card {
      max-inline-size: 100%;
    }
  `,
  template: `
    <app-gallery-component page="File Upload" set="File Upload">
      <ng-template appGallerySpecimen let-values>
        <div class="unit">
          <span class="name">{{ words().label }}</span>
          @switch (values['State']) {
            @case ('Uploading') {
              <scs-file-card [name]="words().report" type="pdf" state="uploading" [percent]="62" />
            }
            @case ('Success') {
              <scs-file-card [name]="words().report" type="pdf" state="ready" [meta]="words().uploaded" />
            }
            @case ('Error') {
              <scs-file-card name="large-scan.pdf" type="pdf" state="error" [reason]="words().failed" />
            }
            @default {
              <scs-file-upload kind="dev.sample" releaseWords [dragging]="values['State'] === 'Dragover'" />
            }
          }
        </div>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="unit">
          <span class="name">{{ words().label }}</span>
          <scs-file-upload kind="dev.sample" releaseWords [uploading]="percent()" (chosen)="upload($event)" />
          @if (file(); as chosen) {
            <scs-file-card
              [name]="chosen"
              type="other"
              [state]="percent() === null ? 'ready' : 'uploading'"
              [percent]="percent() ?? 100"
              [meta]="words().uploaded"
              (remove)="file.set(null)"
            />
          }
        </div>
      </ng-template>
    </app-gallery-component>
  `,
})
export class FileUploadSection {
  private readonly state = inject(GalleryState);
  /** The page's words (File Upload 157:72 and its Arabic twin 304:440; its English leftovers put in Arabic). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { label: 'المرفق', report: 'تقرير-العيادة.pdf', uploaded: '2.4 ميجابايت · تم الرفع', failed: 'تعذّر الرفع — الملف أكبر من 10 ميجابايت' }
      : { label: 'Attachment', report: 'clinic-report.pdf', uploaded: '2.4 MB · Uploaded', failed: 'Upload failed — file exceeds 10MB' },
  );

  protected readonly file = signal<string | null>(null);
  protected readonly percent = signal<number | null>(null);

  /** A dropped file "uploads" in a second and a half, as a real one would. */
  protected upload(files: File[]): void {
    if (!files.length) return;
    this.file.set(files[0].name);
    this.percent.set(0);
    const started = Date.now();
    const tick = setInterval(() => {
      const share = Math.min(100, Math.round(((Date.now() - started) / 1500) * 100));
      this.percent.set(share);
      if (share >= 100) {
        clearInterval(tick);
        this.percent.set(null);
      }
    }, 100);
  }
}
