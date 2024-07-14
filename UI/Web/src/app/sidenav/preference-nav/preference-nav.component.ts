import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {TranslocoDirective} from "@ngneat/transloco";
import {AsyncPipe, NgClass} from "@angular/common";
import {NavService} from "../../_services/nav.service";
import {AccountService, Role} from "../../_services/account.service";
import {SideNavItemComponent} from "../_components/side-nav-item/side-nav-item.component";
import {ActivatedRoute, RouterLink} from "@angular/router";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SettingFragmentPipe} from "../../_pipes/setting-fragment.pipe";

export enum SettingsTabId {

  // Admin
  General = 'admin-general',
  Email = 'admin-email',
  Media = 'admin-media',
  Users = 'admin-users',
  Libraries = 'admin-libraries',
  System = 'admin-system',
  Tasks = 'admin-tasks',
  Statistics = 'admin-statistics',
  KavitaPlus = 'admin-kavitaplus',

  // Non-Admin
  Account = 'account',
  Preferences = 'preferences',
  Clients = 'clients',
  Theme = 'theme',
  Devices = 'devices',
  UserStats = 'user-stats',
  Scrobbling = 'scrobbling'
}

class SideNavItem {
  fragment: SettingsTabId;
  roles: Array<Role> = [];

  constructor(fragment: SettingsTabId, roles: Array<Role> = []) {
    this.fragment = fragment;
    this.roles = roles;
  }
}


interface Tab {title: string, fragment: string};

@Component({
  selector: 'app-preference-nav',
  standalone: true,
  imports: [
    TranslocoDirective,
    NgClass,
    AsyncPipe,
    SideNavItemComponent,
    RouterLink,
    SettingFragmentPipe
  ],
  templateUrl: './preference-nav.component.html',
  styleUrl: './preference-nav.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PreferenceNavComponent {

  private readonly destroyRef = inject(DestroyRef);
  protected readonly navService = inject(NavService);
  protected readonly accountService = inject(AccountService);
  protected readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);

  protected readonly SettingsTabId = SettingsTabId;

  sections = [
    {
      title: 'account-section-title',
      children: [
        new SideNavItem(SettingsTabId.Account, [Role.Admin]),
        new SideNavItem(SettingsTabId.Preferences),
        new SideNavItem(SettingsTabId.Clients),
        new SideNavItem(SettingsTabId.Theme),
        new SideNavItem(SettingsTabId.Devices),
        new SideNavItem(SettingsTabId.UserStats),
        new SideNavItem(SettingsTabId.Scrobbling),
      ]
    },
    {
      title: 'server-section-title',
      children: [
        new SideNavItem(SettingsTabId.General, [Role.Admin]),
        new SideNavItem(SettingsTabId.Users, [Role.Admin]),
        new SideNavItem(SettingsTabId.Libraries, [Role.Admin]),
        new SideNavItem(SettingsTabId.Media, [Role.Admin]),
        new SideNavItem(SettingsTabId.Email, [Role.Admin]),
        new SideNavItem(SettingsTabId.Tasks, [Role.Admin]),
        new SideNavItem(SettingsTabId.Statistics, [Role.Admin]),
        new SideNavItem(SettingsTabId.System, [Role.Admin]),
        new SideNavItem(SettingsTabId.KavitaPlus, [Role.Admin]),
      ]
    }
  ];

  // adminTabs: Array<Tab> = [
  //   {title: 'general-tab', fragment: SettingsTabId.General},
  //   {title: 'users-tab', fragment: SettingsTabId.Users},
  //   {title: 'libraries-tab', fragment: SettingsTabId.Libraries},
  //   {title: 'media-tab', fragment: SettingsTabId.Media},
  //   {title: 'email-tab', fragment: SettingsTabId.Email},
  //   {title: 'tasks-tab', fragment: SettingsTabId.Tasks},
  //   {title: 'statistics-tab', fragment: SettingsTabId.Statistics},
  //   {title: 'system-tab', fragment: SettingsTabId.System},
  //   {title: 'kavita+-tab', fragment: SettingsTabId.KavitaPlus},
  // ];
  //
  // prefTabs: Array<Tab> = [
  //   {title: 'account-tab', fragment: SettingsTabId.Account},
  //   {title: 'preferences-tab', fragment: SettingsTabId.Preferences},
  //   {title: '3rd-party-clients-tab', fragment: SettingsTabId.Clients},
  //   {title: 'theme-tab', fragment: SettingsTabId.Theme},
  //   {title: 'devices-tab', fragment: SettingsTabId.Devices},
  //   {title: 'stats-tab', fragment: SettingsTabId.UserStats},
  // ];
  // // TODO: Title isn't needed as we can map with the fragment

  //active = this.prefTabs[0];
  hasActiveLicense = false;

  constructor() {
    this.accountService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res) {
        // if (this.prefTabs.filter(t => t.fragment == SettingsTabId.Scrobbling).length === 0) {
        //   this.prefTabs.push({title: 'scrobbling-tab', fragment: SettingsTabId.Scrobbling});
        // }

        const items = this.sections[0].children;
        if (items.filter(t => t.fragment == SettingsTabId.Scrobbling).length === 0) {
          items.push(new SideNavItem(SettingsTabId.Scrobbling));
          this.cdRef.markForCheck();
        }

        this.hasActiveLicense = true;
        this.cdRef.markForCheck();
      }


      // this.route.fragment.subscribe(frag => {
      //   const tabs = [...this.adminTabs, ...this.prefTabs];
      //   const tab = tabs.filter(item => item.fragment === frag);
      //   if (tab.length > 0) {
      //     this.active = tab[0];
      //   } else {
      //     this.active = this.prefTabs[1]; // Default to preferences
      //   }
      //   this.cdRef.markForCheck();
      // });
    });
  }

}
