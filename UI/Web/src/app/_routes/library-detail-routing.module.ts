import {Routes} from '@angular/router';
import {AuthGuard} from '../_guards/auth.guard';
import {LibraryAccessGuard} from '../_guards/library-access.guard';
import {LibraryDetailComponent} from '../library-detail/library-detail.component';
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";
import {libraryResolver} from "../_resolvers/library.resolver";


export const routes: Routes = [
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, LibraryAccessGuard],
    component: LibraryDetailComponent,
    resolve: {
      filter: UrlFilterResolver,
      library: libraryResolver
    },
  },
];
