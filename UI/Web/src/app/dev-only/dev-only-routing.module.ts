import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { ThemeTestComponent } from './theme-test/theme-test.component';


const routes: Routes = [
  {
    path: '', 
    component: ThemeTestComponent,
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes), ],
  exports: [RouterModule]
})
export class DevOnlyRoutingModule { }
