import {Routes} from "@angular/router";
import {AllSeriesComponent} from "../all-series/_components/all-series/all-series.component";
import {UrlFilterResolver} from "../_resolvers/url-filter.resolver";


export const routes: Routes = [
  {path: '', component: AllSeriesComponent, pathMatch: 'full',
    runGuardsAndResolvers: 'always',
    resolve: {
      filter: UrlFilterResolver
    }
  },
];
