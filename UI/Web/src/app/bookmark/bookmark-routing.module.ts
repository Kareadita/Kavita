import { Routes } from "@angular/router";
import { AuthGuard } from "../_guards/auth.guard";
import { BookmarksComponent } from "./_components/bookmarks/bookmarks.component";

export const routes: Routes = [
  {path: '**', component: BookmarksComponent, pathMatch: 'full', canActivate: [AuthGuard]},
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    children: [
      {path: 'bookmarks', component: BookmarksComponent},
    ]
  }
];
