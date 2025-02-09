import {AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {AsyncPipe, DOCUMENT, NgClass} from "@angular/common";
import {NavService} from "../../_services/nav.service";
import {AccountService, Role} from "../../_services/account.service";
import {SideNavItemComponent} from "../_components/side-nav-item/side-nav-item.component";
import {ActivatedRoute, NavigationEnd, Router} from "@angular/router";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SettingFragmentPipe} from "../../_pipes/setting-fragment.pipe";
import {map, Observable, of, shareReplay, switchMap, take, tap} from "rxjs";
import {ServerService} from "../../_services/server.service";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {User} from "../../_models/user";
import {filter} from "rxjs/operators";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {LicenseService} from "../../_services/license.service";
import {ManageService} from "../../_services/manage.service";
import {MatchStateOption} from "../../_models/kavitaplus/match-state-option";

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
  EmailHistory = 'admin-email-history',

  // Kavita+
  KavitaPlusLicense = 'admin-kavitaplus',
  MALStackImport = 'mal-stack-import',
  MatchedMetadata = 'admin-matched-metadata',
  ManageUserTokens = 'admin-manage-tokens',
  Metadata = 'admin-metadata',

  // Non-Admin
  Account = 'account',
  Preferences = 'preferences',
  Clients = 'clients',
  Theme = 'theme',
  Devices = 'devices',
  UserStats = 'user-stats',
  Scrobbling = 'scrobbling',
  ScrobblingHolds = 'scrobble-holds',
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
  /**
   * If you have any of these, the item will be restricted
   */
  restrictRoles: Array<Role> = [];
  badgeCount$?: Observable<number> | undefined;

  constructor(fragment: SettingsTabId, roles: Array<Role> = [], badgeCount$: Observable<number> | undefined = undefined, restrictRoles: Array<Role> = []) {
    this.fragment = fragment;
    this.roles = roles;
    this.restrictRoles = restrictRoles;
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
  protected readonly licenseService = inject(LicenseService);
  protected readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly serverService = inject(ServerService);
  private readonly scrobbleService = inject(ScrobblingService);
  private readonly router = inject(Router);
  protected readonly utilityService = inject(UtilityService);
  private readonly manageService = inject(ManageService);
  private readonly document = inject(DOCUMENT);

  hasActiveLicense = false;
  /**
   * This links to settings.component.html which has triggers on what underlying component to render out.
   */
  sections: Array<PrefSection> = [
    {
      title: 'account-section-title',
      children: [
        new SideNavItem(SettingsTabId.Account, []),
        new SideNavItem(SettingsTabId.Preferences),
        new SideNavItem(SettingsTabId.Customize, [], undefined, [Role.ReadOnly]),
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
        new SideNavItem(SettingsTabId.Users, [Role.Admin]),
        new SideNavItem(SettingsTabId.Libraries, [Role.Admin]),
        new SideNavItem(SettingsTabId.Tasks, [Role.Admin]),
      ]
    },
    {
      title: 'import-section-title',
      children: [
        new SideNavItem(SettingsTabId.CBLImport, [], undefined, [Role.ReadOnly]),
      ]
    },
    {
      title: 'info-section-title',
      children: [
        new SideNavItem(SettingsTabId.System, [Role.Admin]),
        new SideNavItem(SettingsTabId.Statistics, [Role.Admin]),
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
        new SideNavItem(SettingsTabId.EmailHistory, [Role.Admin]),
      ]
    },
    {
      title: 'kavitaplus-section-title',
      children: [
        new SideNavItem(SettingsTabId.KavitaPlusLicense, [Role.Admin])
        // All other sections added dynamically
      ]
    }
  ];
  collapseSideNavOnMobileNav$ = this.router.events.pipe(
    filter(event => event instanceof NavigationEnd),
    takeUntilDestroyed(this.destroyRef),
    map(evt => evt as NavigationEnd),
    switchMap(_ => this.utilityService.activeBreakpoint$),
    filter((b) => b < Breakpoint.Tablet),
    switchMap(() => this.navService.sideNavCollapsed$),
    take(1),
    filter(collapsed => !collapsed),
    tap(c => {
      this.navService.collapseSideNav(true);
    }),
  );

  private readonly matchedMetadataBadgeCount$ = this.accountService.currentUser$.pipe(
    take(1),
    switchMap(user => {
      if (!user || !this.accountService.hasAdminRole(user)) {
        // If no user or user does not have the admin role, return an observable of -1
        return of(-1);
      } else {
        return this.manageService.getAllKavitaPlusSeries({
          matchStateOption: MatchStateOption.Error,
          searchTerm: ''
        }).pipe(
          takeUntilDestroyed(this.destroyRef),
          map(d => d.length),
          shareReplay({bufferSize: 1, refCount: true})
        );
      }
    })
  );

  private readonly scrobblingErrorBadgeCount$ = this.accountService.currentUser$.pipe(
    take(1),
    switchMap(user => {
      if (!user || !this.accountService.hasAdminRole(user)) {
        // If no user or user does not have the admin role, return an observable of -1
        return of(-1);
      } else {
        return this.scrobbleService.getScrobbleErrors().pipe(
          takeUntilDestroyed(this.destroyRef),
          map(d => d.length),
          shareReplay({bufferSize: 1, refCount: true})
        );
      }
    })
  );

  constructor() {
    this.collapseSideNavOnMobileNav$.subscribe();

    // Ensure that on mobile, we are collapsed by default
    if (this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet) {
      this.navService.collapseSideNav(true);
    }

    this.licenseService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      this.hasActiveLicense = res;
      if (res) {
        const kavitaPlusSection = this.sections[4];
        if (kavitaPlusSection.children.length === 1) {
          kavitaPlusSection.children.push(new SideNavItem(SettingsTabId.ManageUserTokens, [Role.Admin]));
          kavitaPlusSection.children.push(new SideNavItem(SettingsTabId.Metadata, [Role.Admin]));

          // Keep all setting type of screens above this line
          kavitaPlusSection.children.push(new SideNavItem(SettingsTabId.MatchedMetadata, [Role.Admin],
            this.matchedMetadataBadgeCount$
          ));

          // Scrobbling History needs to be per-user and allow admin to view all
          kavitaPlusSection.children.push(new SideNavItem(SettingsTabId.ScrobblingHolds, []));
          kavitaPlusSection.children.push(new SideNavItem(SettingsTabId.Scrobbling, [], this.scrobblingErrorBadgeCount$)
          );
        }

        if (this.sections[2].children.length === 1) {
          this.sections[2].children.push(new SideNavItem(SettingsTabId.MALStackImport, []));
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
    // Filter out items where the user has a restricted role
    const visibleItems = section.children.filter(item =>
      item.restrictRoles.length === 0 || !this.accountService.hasAnyRole(user, item.restrictRoles)
    );

    // Check if the user has any allowed roles in the remaining items
    return visibleItems.some(item =>
      this.accountService.hasAnyRole(user, item.roles)
    );
  }

  collapse() {
    this.navService.toggleSideNav();
  }

  protected readonly Breakpoint = Breakpoint;
}
