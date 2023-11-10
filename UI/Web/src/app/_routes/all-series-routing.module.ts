import { Routes } from "@angular/router";
import { AllSeriesComponent } from "../all-series/_components/all-series/all-series.component";


export const routes: Routes = [
  {path: '', component: AllSeriesComponent, pathMatch: 'full'},
];
