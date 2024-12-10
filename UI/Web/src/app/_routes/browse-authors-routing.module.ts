import { Routes } from "@angular/router";
import { AllSeriesComponent } from "../all-series/_components/all-series/all-series.component";
import {BrowseAuthorsComponent} from "../browse-people/browse-authors.component";


export const routes: Routes = [
  {path: '', component: BrowseAuthorsComponent, pathMatch: 'full'},
];
