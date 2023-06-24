import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AnnouncementsComponent } from './_components/announcements/announcements.component';
import { ChangelogComponent } from './_components/changelog/changelog.component';
import { AnnouncementsRoutingModule } from './announcements-routing.module';
import { SharedModule } from '../shared/shared.module';
import { SidenavModule } from '../sidenav/sidenav.module';
import {ReadMoreComponent} from "../shared/read-more/read-more.component";



@NgModule({
  declarations: [
    AnnouncementsComponent,
    ChangelogComponent
  ],
    imports: [
        CommonModule,
        AnnouncementsRoutingModule,
        SharedModule,
        SidenavModule,
        ReadMoreComponent
    ]
})
export class AnnouncementsModule { }
