import {Routes} from "@angular/router";
import {BookmarksComponent} from "../bookmark/_components/bookmarks/bookmarks.component";
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";

export const routes: Routes = [
  {path: '', component: BookmarksComponent, pathMatch: 'full',
    resolve: {
      filter: UrlFilterResolver
    },
    runGuardsAndResolvers: 'always',
  },
];
