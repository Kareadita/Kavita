import { NgModule } from "@angular/core";
import { Routes, RouterModule } from "@angular/router";
import { AuthGuard } from "../_guards/auth.guard";
import { AllSeriesComponent } from "./_components/all-series/all-series.component";


const routes: Routes = [
  {path: '**', component: AllSeriesComponent, pathMatch: 'full', canActivate: [AuthGuard]},
  {
    path: '',
    component: AllSeriesComponent,
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AllSeriesRoutingModule { }