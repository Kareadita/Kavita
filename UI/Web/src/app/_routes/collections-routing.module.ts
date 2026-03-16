import {Routes} from '@angular/router';
import {AllCollectionsComponent} from '../collections/_components/all-collections/all-collections.component';
import {CollectionDetailComponent} from '../collections/_components/collection-detail/collection-detail.component';
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";
import {collectionResolver} from "../_resolvers/collection.resolver";

export const routes: Routes = [
  {path: '', component: AllCollectionsComponent, pathMatch: 'full', title: 'title.collections'},
  {path: ':collectionId', component: CollectionDetailComponent,
    data: {titleField: 'collection', titleProp: 'title'},
    resolve: {
      collection: collectionResolver,
      filter: UrlFilterResolver
    },
    runGuardsAndResolvers: 'always',
  },
];
