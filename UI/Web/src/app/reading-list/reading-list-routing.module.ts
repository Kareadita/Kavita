import { NgModule } from "@angular/core";
import { Routes, RouterModule } from "@angular/router";
import { AuthGuard } from "../_guards/auth.guard";
import { ReadingListDetailComponent } from "./_components/reading-list-detail/reading-list-detail.component";
import { ReadingListsComponent } from "./_components/reading-lists/reading-lists.component";


const routes: Routes = [
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


@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ReadingListRoutingModule { }
