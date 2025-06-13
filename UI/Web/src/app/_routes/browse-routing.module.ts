import {Routes} from "@angular/router";
import {BrowsePeopleComponent} from "../browse/browse-people/browse-people.component";
import {BrowseGenresComponent} from "../browse/browse-genres/browse-genres.component";
import {BrowseTagsComponent} from "../browse/browse-tags/browse-tags.component";
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";


export const routes: Routes = [
  // Legacy route
  {path: 'authors', component: BrowsePeopleComponent, pathMatch: 'full',
    resolve: {
      filter: UrlFilterResolver
    },
    runGuardsAndResolvers: 'always',
  },
  {path: 'people', component: BrowsePeopleComponent, pathMatch: 'full',
    resolve: {
      filter: UrlFilterResolver
    },
    runGuardsAndResolvers: 'always',
  },
  {path: 'genres', component: BrowseGenresComponent, pathMatch: 'full'},
  {path: 'tags', component: BrowseTagsComponent, pathMatch: 'full'},
];
