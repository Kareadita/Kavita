import { Routes } from "@angular/router";
import { AuthGuard } from "../_guards/auth.guard";
import { AllSeriesComponent } from "../all-series/_components/all-series/all-series.component";


export const routes: Routes = [
  {path: '**', component: AllSeriesComponent, pathMatch: 'full', canActivate: [AuthGuard]},
  {
    path: '',
    component: AllSeriesComponent,
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
  }
];
