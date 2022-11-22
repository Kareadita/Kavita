import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { WantToReadComponent } from './_components/want-to-read/want-to-read.component';

const routes: Routes = [
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


@NgModule({
  imports: [RouterModule.forChild(routes), ],
  exports: [RouterModule]
})
export class WantToReadRoutingModule { }
