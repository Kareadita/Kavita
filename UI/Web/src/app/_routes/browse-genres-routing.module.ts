import {Routes} from "@angular/router";
import {BrowseGenresComponent} from "../all-genres/browse-genres.component";


export const routes: Routes = [
  {path: '', component: BrowseGenresComponent, pathMatch: 'full'},
];
