import { Routes } from '@angular/router';
import { WantToReadComponent } from '../want-to-read/_components/want-to-read/want-to-read.component';

export const routes: Routes = [
  {path: '', component: WantToReadComponent, pathMatch: 'full'},
];
