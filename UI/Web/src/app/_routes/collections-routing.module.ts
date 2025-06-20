import {Routes} from '@angular/router';
import {AllCollectionsComponent} from '../collections/_components/all-collections/all-collections.component';
import {CollectionDetailComponent} from '../collections/_components/collection-detail/collection-detail.component';
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";

export const routes: Routes = [
  {path: '', component: AllCollectionsComponent, pathMatch: 'full'},
  {path: ':id', component: CollectionDetailComponent,
    resolve: {
      filter: UrlFilterResolver
    },
    runGuardsAndResolvers: 'always',
  },
];

