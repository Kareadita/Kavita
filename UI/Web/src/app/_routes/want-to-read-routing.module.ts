import {Routes} from '@angular/router';
import {WantToReadComponent} from '../want-to-read/_components/want-to-read/want-to-read.component';
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";

export const routes: Routes = [
  {path: '', component: WantToReadComponent, pathMatch: 'full', runGuardsAndResolvers: 'always', resolve: {
    filter: UrlFilterResolver
    }
  },
];
