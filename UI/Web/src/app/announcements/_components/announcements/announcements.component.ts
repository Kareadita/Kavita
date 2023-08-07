import { Component } from '@angular/core';
import { ChangelogComponent } from '../changelog/changelog.component';
import { SideNavCompanionBarComponent } from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoModule} from "@ngneat/transloco";

@Component({
    selector: 'app-announcements',
    templateUrl: './announcements.component.html',
    styleUrls: ['./announcements.component.scss'],
    standalone: true,
  imports: [SideNavCompanionBarComponent, ChangelogComponent, TranslocoModule]
})
export class AnnouncementsComponent {

  constructor() { }

}
