import { Routes } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { WantToReadComponent } from '../want-to-read/_components/want-to-read/want-to-read.component';

export const routes: Routes = [
    {path: '**', component: WantToReadComponent, pathMatch: 'full'},
    {
      path: '',
      runGuardsAndResolvers: 'always',
      canActivate: [AuthGuard],
      children: [
        {path: '', component: WantToReadComponent, pathMatch: 'full'},
      ]
    }
];
