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
  MediaIssues = 'admin-media-issues',

  // Kavita+
  KavitaPlus = 'admin-kavitaplus',
  MALStackImport = 'mal-stack-import',

  // Non-Admin
  Account = 'account',
  Preferences = 'preferences',
  Clients = 'clients',
  Theme = 'theme',
  Devices = 'devices',
  UserStats = 'user-stats',
  Scrobbling = 'scrobbling',
  Customize = 'customize',
  CBLImport = 'cbl-import'
}

class SideNavItem {
  fragment: SettingsTabId;
  roles: Array<Role> = [];

  constructor(fragment: SettingsTabId, roles: Array<Role> = []) {
    this.fragment = fragment;
    this.roles = roles;
  }
}

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

  /**
   * This links to settings.component.html which has triggers on what underlying component to render out.
   */
  sections = [
    {
      title: 'account-section-title',
      children: [
        new SideNavItem(SettingsTabId.Account, [Role.Admin]),
        new SideNavItem(SettingsTabId.Preferences),
        new SideNavItem(SettingsTabId.Customize),
        new SideNavItem(SettingsTabId.Clients),
        new SideNavItem(SettingsTabId.Theme),
        new SideNavItem(SettingsTabId.Devices),
        new SideNavItem(SettingsTabId.UserStats),
      ]
    },
    {
      title: 'server-section-title',
      children: [
        new SideNavItem(SettingsTabId.General, [Role.Admin]),
        new SideNavItem(SettingsTabId.Media, [Role.Admin]),
        new SideNavItem(SettingsTabId.Email, [Role.Admin]),
        new SideNavItem(SettingsTabId.Statistics, [Role.Admin]),
        new SideNavItem(SettingsTabId.System, [Role.Admin]),

      ]
    },
    {
      title: 'manage-section-title',
      children: [
        new SideNavItem(SettingsTabId.Users, [Role.Admin]),
        new SideNavItem(SettingsTabId.Libraries, [Role.Admin]),
        new SideNavItem(SettingsTabId.MediaIssues, [Role.Admin]), // NOTE: It might be cool to pass an observable here for getting a count of issues to put in a badge
        new SideNavItem(SettingsTabId.Tasks, [Role.Admin]),
      ]
    },
    {
      title: 'import-section-title',
      children: [
        new SideNavItem(SettingsTabId.CBLImport, []),
      ]
    },
    {
      title: 'kavitaplus-section-title',
      children: [
        new SideNavItem(SettingsTabId.KavitaPlus, [Role.Admin]),
      ]
    }
  ];


  hasActiveLicense = false;

  constructor() {
    this.accountService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res) {
        this.hasActiveLicense = true;
        if (this.hasActiveLicense) {
          if (this.sections[4].children.length === 1) {
            this.sections[4].children.push(new SideNavItem(SettingsTabId.Scrobbling, []));
          }
          if (this.sections[3].children.length === 1) {
            this.sections[3].children.push(new SideNavItem(SettingsTabId.MALStackImport, []));
          }

        }

        this.cdRef.markForCheck();
      }
    });
  }

}
