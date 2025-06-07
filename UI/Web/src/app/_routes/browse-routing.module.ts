import {Routes} from "@angular/router";
import {BrowseAuthorsComponent} from "../browse/browse-people/browse-authors.component";
import {BrowseGenresComponent} from "../browse/browse-genres/browse-genres.component";
import {BrowseTagsComponent} from "../browse/browse-tags/browse-tags.component";


export const routes: Routes = [
  {path: 'authors', component: BrowseAuthorsComponent, pathMatch: 'full'},
  {path: 'genres', component: BrowseGenresComponent, pathMatch: 'full'},
  {path: 'tags', component: BrowseTagsComponent, pathMatch: 'full'},
];
