import { Routes } from "@angular/router";
import { AdminGuard } from "../_guards/admin.guard";
import { AuthGuard } from "../_guards/auth.guard";
import { AnnouncementsComponent } from "../announcements/_components/announcements/announcements.component";

export const routes: Routes = [
  {path: '**', component: AnnouncementsComponent, pathMatch: 'full', canActivate: [AuthGuard, AdminGuard]},
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, AdminGuard],
    children: [
      {path: 'announcements', component: AnnouncementsComponent},
    ]
  }
];
