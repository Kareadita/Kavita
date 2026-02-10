import {Routes} from '@angular/router';
import {LibraryDetailComponent} from '../library-detail/library-detail.component';
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";


export const routes: Routes = [
  {
    path: '',
    component: LibraryDetailComponent,
    resolve: {
      filter: UrlFilterResolver
    }
  }
];
