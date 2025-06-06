import {Routes} from "@angular/router";
import {BrowseAuthorsComponent} from "../browse-people/browse-authors.component";


export const routes: Routes = [
  {path: '', component: BrowseAuthorsComponent, pathMatch: 'full'},
];
