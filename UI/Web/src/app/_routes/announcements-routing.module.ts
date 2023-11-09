import { Routes } from "@angular/router";
import { AnnouncementsComponent } from "../announcements/_components/announcements/announcements.component";

export const routes: Routes = [
  {path: '', component: AnnouncementsComponent, pathMatch: 'full'},
];
