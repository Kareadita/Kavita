import {ChangeDetectionStrategy, Component, DestroyRef, inject} from '@angular/core';
import {TranslocoDirective} from "@ngneat/transloco";
import {AsyncPipe, NgClass} from "@angular/common";
import {NavService} from "../../_services/nav.service";
import {AccountService} from "../../_services/account.service";
import {SideNavItemComponent} from "../_components/side-nav-item/side-nav-item.component";
import {RouterLink} from "@angular/router";


enum AdminTabId {
  General = '',
  Email = 'email',
  Media = 'media',
  Users = 'users',
  Libraries = 'libraries',
  System = 'system',
  Tasks = 'tasks',
  Statistics = 'statistics',
  KavitaPlus = 'kavitaplus'
}

enum PreferenceTabId {
  Account = 'account',
  Preferences = '',
  Clients = 'clients',
  Theme = 'theme',
  Devices = 'devices',
  Stats = 'stats',
  Scrobbling = 'scrobbling'
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
    RouterLink
  ],
  templateUrl: './preference-nav.component.html',
  styleUrl: './preference-nav.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PreferenceNavComponent {

  private readonly destroyRef = inject(DestroyRef);
  protected readonly navService = inject(NavService);
  protected readonly accountService = inject(AccountService);

  protected readonly AdminTabId = AdminTabId;

  adminTabs: Array<Tab> = [
    {title: 'general-tab', fragment: AdminTabId.General},
    {title: 'users-tab', fragment: AdminTabId.Users},
    {title: 'libraries-tab', fragment: AdminTabId.Libraries},
    {title: 'media-tab', fragment: AdminTabId.Media},
    {title: 'email-tab', fragment: AdminTabId.Email},
    {title: 'tasks-tab', fragment: AdminTabId.Tasks},
    {title: 'statistics-tab', fragment: AdminTabId.Statistics},
    {title: 'system-tab', fragment: AdminTabId.System},
    {title: 'kavita+-tab', fragment: AdminTabId.KavitaPlus},
  ];

  prefTabs: Array<Tab> = [
    {title: 'account-tab', fragment: PreferenceTabId.Account},
    {title: 'preferences-tab', fragment: PreferenceTabId.Preferences},
    {title: '3rd-party-clients-tab', fragment: PreferenceTabId.Clients},
    {title: 'theme-tab', fragment: PreferenceTabId.Theme},
    {title: 'devices-tab', fragment: PreferenceTabId.Devices},
    {title: 'stats-tab', fragment: PreferenceTabId.Stats},
  ];

  active = this.adminTabs[0];

}
