import { Injectable, computed, signal } from '@angular/core';

/**
 * What a page holds that a person has not saved yet and that reading the page again would
 * lose — a file chosen and not yet saved with its record (spec 009, US4). The shell asks
 * before it draws a page anew when the connection returns (spec 006, FR-021).
 */
@Injectable({ providedIn: 'root' })
export class UnsavedWork {
  private readonly holders = signal<readonly (() => boolean)[]>([]);

  /** True while any page holds something unsaved. */
  readonly any = computed(() => this.holders().some((holds) => holds()));

  /** Registers a holder; the function returned takes it out again. */
  hold(holds: () => boolean): () => void {
    this.holders.update((all) => [...all, holds]);
    return () => this.holders.update((all) => all.filter((each) => each !== holds));
  }
}
