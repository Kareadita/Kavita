import { NgModule } from "@angular/core";
import { Routes, RouterModule } from "@angular/router";
import { AdminGuard } from "../_guards/admin.guard";
import { AuthGuard } from "../_guards/auth.guard";
import { AnnouncementsComponent } from "./announcements.component";

const routes: Routes = [
  {path: '**', component: AnnouncementsComponent, pathMatch: 'full', canActivate: [AuthGuard, AdminGuard]},
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, AdminGuard],
    children: [
      {path: 'announcments', component: AnnouncementsComponent},
    ]
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AnnouncementsRoutingModule { }