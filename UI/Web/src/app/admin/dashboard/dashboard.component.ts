import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {ActivatedRoute, RouterLink} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {ServerService} from 'src/app/_services/server.service';
import {Title} from '@angular/platform-browser';
import {NavService} from '../../_services/nav.service';
import {SentenceCasePipe} from '../../pipe/sentence-case.pipe';
import {LicenseComponent} from '../license/license.component';
import {ManageTasksSettingsComponent} from '../manage-tasks-settings/manage-tasks-settings.component';
import {ServerStatsComponent} from '../../statistics/_components/server-stats/server-stats.component';
import {ManageSystemComponent} from '../manage-system/manage-system.component';
import {ManageLogsComponent} from '../manage-logs/manage-logs.component';
import {ManageLibraryComponent} from '../manage-library/manage-library.component';
import {ManageUsersComponent} from '../manage-users/manage-users.component';
import {ManageMediaSettingsComponent} from '../manage-media-settings/manage-media-settings.component';
import {ManageEmailSettingsComponent} from '../manage-email-settings/manage-email-settings.component';
import {ManageSettingsComponent} from '../manage-settings/manage-settings.component';
import {NgFor, NgIf} from '@angular/common';
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavOutlet} from '@ng-bootstrap/ng-bootstrap';
import {
  SideNavCompanionBarComponent
} from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";

enum TabID {
  General = '',
  Email = 'email',
  Media = 'media',
  Users = 'users',
  Libraries = 'libraries',
  System = 'system',
  Tasks = 'tasks',
  Logs = 'logs',
  Statistics = 'statistics',
  KavitaPlus = 'kavitaplus'
}

@Component({
    selector: 'app-dashboard',
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.scss'],
    standalone: true,
  imports: [SideNavCompanionBarComponent, NgbNav, NgFor, NgbNavItem, NgbNavItemRole, NgbNavLink, RouterLink, NgbNavContent, NgIf, ManageSettingsComponent, ManageEmailSettingsComponent, ManageMediaSettingsComponent, ManageUsersComponent, ManageLibraryComponent, ManageLogsComponent, ManageSystemComponent, ServerStatsComponent, ManageTasksSettingsComponent, LicenseComponent, NgbNavOutlet, SentenceCasePipe, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'general-tab', fragment: TabID.General},
    {title: 'users-tab', fragment: TabID.Users},
    {title: 'libraries-tab', fragment: TabID.Libraries},
    //{title: 'logs-tab', fragment: TabID.Logs},
    {title: 'media-tab', fragment: TabID.Media},
    {title: 'email-tab', fragment: TabID.Email},
    {title: 'tasks-tab', fragment: TabID.Tasks},
    {title: 'statistics-tab', fragment: TabID.Statistics},
    {title: 'system-tab', fragment: TabID.System},
    {title: 'kavita+-tab', fragment: TabID.KavitaPlus},
  ];
  active = this.tabs[0];

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly translocoService = inject(TranslocoService);

  get TabID() {
    return TabID;
  }

  constructor(public route: ActivatedRoute, private serverService: ServerService,
    private toastr: ToastrService, private titleService: Title, public navService: NavService) {
    this.route.fragment.subscribe(frag => {
      const tab = this.tabs.filter(item => item.fragment === frag);
      if (tab.length > 0) {
        this.active = tab[0];
      } else {
        this.active = this.tabs[0]; // Default to first tab
      }
      this.cdRef.markForCheck();
    });

  }

  ngOnInit() {
    this.titleService.setTitle('Kavita - ' + this.translocoService.translate('admin-dashboard.title'));
  }
}
