import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AnnouncementsComponent } from './_components/announcements/announcements.component';
import { ChangelogComponent } from './_components/changelog/changelog.component';
import { AnnouncementsRoutingModule } from './announcements-routing.module';
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";



@NgModule({
    imports: [
        CommonModule,
        AnnouncementsRoutingModule,
        ReadMoreComponent,
        LoadingComponent,
        SideNavCompanionBarComponent,
        AnnouncementsComponent,
        ChangelogComponent
    ]
})
export class AnnouncementsModule { }
