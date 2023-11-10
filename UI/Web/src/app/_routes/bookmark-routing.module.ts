import { Routes } from "@angular/router";
import { BookmarksComponent } from "../bookmark/_components/bookmarks/bookmarks.component";

export const routes: Routes = [
  {path: '', component: BookmarksComponent, pathMatch: 'full'},
];
