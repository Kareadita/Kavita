import { Routes } from "@angular/router";
import { AuthGuard } from "../_guards/auth.guard";
import { ReadingListDetailComponent } from "../reading-list/_components/reading-list-detail/reading-list-detail.component";
import { ReadingListsComponent } from "../reading-list/_components/reading-lists/reading-lists.component";


export const routes: Routes = [
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    children: [
        {path: '', component: ReadingListsComponent, pathMatch: 'full'},
        {path: ':id', component: ReadingListDetailComponent, pathMatch: 'full'},
    ]
  },
  {path: '**', component: ReadingListsComponent, pathMatch: 'full', canActivate: [AuthGuard]},
];
