import { NgModule } from "@angular/core";
import { Routes, RouterModule } from "@angular/router";
import { AuthGuard } from "../_guards/auth.guard";
import { BookmarksComponent } from "./bookmarks/bookmarks.component";

const routes: Routes = [
  {path: '**', component: BookmarksComponent, pathMatch: 'full', canActivate: [AuthGuard]},
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    children: [
      {path: '/bookmarks', component: BookmarksComponent},
    ]
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class BookmarkRoutingModule { }