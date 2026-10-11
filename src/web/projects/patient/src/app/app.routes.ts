import { Routes } from '@angular/router';
import { ClinicShellComponent } from './clinic/clinic-shell.component';
import { NotAvailablePage } from './clinic/not-available.page';

/**
 * The patient site is served at `scs-heta.com/<clinic>`: the first part of every address is a
 * clinic's short name. The bare address names no clinic.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', component: NotAvailablePage },
  { path: ':clinic', children: [{ path: '**', component: ClinicShellComponent }] },
];
