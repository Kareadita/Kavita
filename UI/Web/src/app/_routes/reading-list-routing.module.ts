import {Routes} from "@angular/router";
import {
  ReadingListDetailComponent
} from "../reading-list/_components/reading-list-detail/reading-list-detail.component";
import {ReadingListsComponent} from "../reading-list/_components/reading-lists/reading-lists.component";
import {AuthGuard} from "../_guards/auth.guard";
import {readingListResolver} from "../_resolvers/reading-list.resolver";

// TODO: I can't figure out how to use this pattern and have the resolver work for readingList detail page.
export const routes: Routes = [
  {
    path: '',
    component: ReadingListsComponent,
    pathMatch: 'full'
  },
  {
    path: ':readingListId',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    resolve: { readingList: readingListResolver },
    loadComponent: () => import('../reading-list/_components/reading-list-detail/reading-list-detail.component').then(c => ReadingListDetailComponent),
  }
];
