import { NgModule } from "@angular/core";
import { Routes, RouterModule } from "@angular/router";
import { AuthGuard } from "../_guards/auth.guard";
import { ReadingListDetailComponent } from "./reading-list-detail/reading-list-detail.component";

const routes: Routes = [
  {
    path: '', 
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    children: [
        {path: '', component: ReadingListDetailComponent, pathMatch: 'full'},
        // {path: ':id', component: CollectionDetailComponent},
    ]
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes), ],
  exports: [RouterModule]
})
export class ReadingListRoutingModule { }