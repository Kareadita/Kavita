import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AnnouncementsComponent } from './announcements.component';
import { ChangelogComponent } from './changelog/changelog.component';
import { AnnouncementsRoutingModule } from './announcements-routing.module';
import { SharedModule } from '../shared/shared.module';
import { PipeModule } from '../pipe/pipe.module';
import { SideNavModule } from '../sidenav/sidenav.module';



@NgModule({
  declarations: [
    AnnouncementsComponent,
    ChangelogComponent
  ],
  imports: [
    CommonModule,
    AnnouncementsRoutingModule,
    SharedModule,
    PipeModule,
    SideNavModule
  ]
})
export class AnnouncementsModule { }
