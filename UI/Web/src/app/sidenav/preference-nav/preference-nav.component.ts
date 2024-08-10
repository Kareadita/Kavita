import {AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {AsyncPipe, DOCUMENT, NgClass} from "@angular/common";
import {NavService} from "../../_services/nav.service";
import {AccountService, Role} from "../../_services/account.service";
import {SideNavItemComponent} from "../_components/side-nav-item/side-nav-item.component";
import {ActivatedRoute, RouterLink} from "@angular/router";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SettingFragmentPipe} from "../../_pipes/setting-fragment.pipe";
import {map, Observable, of, shareReplay, switchMap, take} from "rxjs";
import {ServerService} from "../../_services/server.service";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {User} from "../../_models/user";

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

interface PrefSection {
  title: string;
  children: SideNavItem[];
}

class SideNavItem {
  fragment: SettingsTabId;
  roles: Array<Role> = [];
  badgeCount$?: Observable<number> | undefined;

  constructor(fragment: SettingsTabId, roles: Array<Role> = [], badgeCount$: Observable<number> | undefined = undefined) {
    this.fragment = fragment;
    this.roles = roles;
    this.badgeCount$ = badgeCount$;
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
export class PreferenceNavComponent implements AfterViewInit {

  private readonly destroyRef = inject(DestroyRef);
  protected readonly navService = inject(NavService);
  protected readonly accountService = inject(AccountService);
  protected readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly serverService = inject(ServerService);
  private readonly scrobbleService = inject(ScrobblingService);
  private readonly document = inject(DOCUMENT);

  /**
   * This links to settings.component.html which has triggers on what underlying component to render out.
   */
  sections: Array<PrefSection> = [
    {
      title: 'account-section-title',
      children: [
        new SideNavItem(SettingsTabId.Account, []),
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
        new SideNavItem(SettingsTabId.MediaIssues, [Role.Admin],
          this.accountService.currentUser$.pipe(
            take(1),
            switchMap(user => {
              if (!user || !this.accountService.hasAdminRole(user)) {
                // If no user or user does not have the admin role, return an observable of -1
                return of(-1);
              } else {
                return this.serverService.getMediaErrors().pipe(
                  takeUntilDestroyed(this.destroyRef),
                  map(d => d.length),
                  shareReplay({ bufferSize: 1, refCount: true })
                );
              }
            })
          )),
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

    this.navService.collapseSideNav(false);

    this.accountService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res) {
        this.hasActiveLicense = true;
        if (this.hasActiveLicense) {
          if (this.sections[4].children.length === 1) {
            this.sections[4].children.push(new SideNavItem(SettingsTabId.Scrobbling, [],
                this.accountService.currentUser$.pipe(
                  take(1),
                  switchMap(user => {
                    if (!user || !this.accountService.hasAdminRole(user)) {
                      // If no user or user does not have the admin role, return an observable of -1
                      return of(-1);
                    } else {
                      return this.scrobbleService.getScrobbleErrors().pipe(
                        takeUntilDestroyed(this.destroyRef),
                        map(d => d.length),
                        shareReplay({ bufferSize: 1, refCount: true })
                      );
                    }
                  })
                ))
            );
          }
          if (this.sections[3].children.length === 1) {
            this.sections[3].children.push(new SideNavItem(SettingsTabId.MALStackImport, []));
          }

        }

        this.scrollToActiveItem();
        this.cdRef.markForCheck();
      }
    });
  }

  ngAfterViewInit() {
    this.scrollToActiveItem();
  }

  scrollToActiveItem() {
    const activeFragment = this.route.snapshot.fragment;
    if (activeFragment) {
      const element = this.document.getElementById('nav-item-' + activeFragment);
      if (element) {
        element.scrollIntoView({behavior: 'smooth', block: 'center'});
      }
    }
  }

  hasAnyChildren(user: User, section: PrefSection) {
    return section.children.filter(item => this.accountService.hasAnyRole(user, item.roles)).length > 0;
  }

}
