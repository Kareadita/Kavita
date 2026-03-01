import {Routes} from '@angular/router';
import {LibraryDetailComponent} from '../library-detail/library-detail.component';
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";
import {libraryResolver} from "../_resolvers/library.resolver";


export const routes: Routes = [
  {
    path: '',
    component: LibraryDetailComponent,
    data: {titleField: 'library', titleProp: 'name'},
    resolve: {
      library: libraryResolver,
      filter: UrlFilterResolver
    }
  }
];
