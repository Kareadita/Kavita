import {Routes} from "@angular/router";
import {AllFiltersComponent} from "../all-filters/all-filters.component";


export const routes: Routes = [
  {path: '', component: AllFiltersComponent, pathMatch: 'full'},
];
