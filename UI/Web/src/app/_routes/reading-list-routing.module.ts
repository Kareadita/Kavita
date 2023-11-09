import { Routes } from "@angular/router";
import { ReadingListDetailComponent } from "../reading-list/_components/reading-list-detail/reading-list-detail.component";
import { ReadingListsComponent } from "../reading-list/_components/reading-lists/reading-lists.component";


export const routes: Routes = [
  {path: '', component: ReadingListsComponent, pathMatch: 'full'},
  {path: ':id', component: ReadingListDetailComponent, pathMatch: 'full'},
];
