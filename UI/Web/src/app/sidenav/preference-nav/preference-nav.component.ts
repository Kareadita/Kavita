import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  effect,
  inject
} from '@angular/core';
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
import {User} from "../../_models/user/user";
import {filter} from "rxjs/operators";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {LicenseService} from "../../_services/license.service";
import {ManageService} from "../../_services/manage.service";
import {MatchStateOption} from "../../_models/kavitaplus/match-state-option";
import {KeyBindService} from "../../_services/key-bind.service";
import {KeyBindTarget} from "../../_models/preferences/preferences";

export enum SettingsTabId {

  // Admin
  Activity = 'admin-activity',
  General = 'admin-general',
  OpenIDConnect = 'admin-oidc',
  Email = 'admin-email',
  Media = 'admin-media',
  Users = 'admin-users',
  Libraries = 'admin-libraries',
  System = 'admin-system',
  Tasks = 'admin-tasks',
  Statistics = 'admin-statistics',
  MediaIssues = 'admin-media-issues',
  EmailHistory = 'admin-email-history',
  ManageMetadata = 'admin-public-metadata',
  AdminDevices = 'admin-device',

  // Kavita+
  KavitaPlusLicense = 'admin-kavitaplus',
  MALStackImport = 'mal-stack-import',
  MappingsImport = 'admin-mappings-import',
  MatchedMetadata = 'admin-matched-metadata',
  ManageUserTokens = 'admin-manage-tokens',
  Metadata = 'admin-metadata',

  // Non-Admin
  Account = 'account',
  Preferences = 'preferences',
  CustomKeyBinds = 'custom-key-binds',
  ReadingProfiles = 'reading-profiles',
  Font = 'font',
  Clients = 'clients',
  Theme = 'theme',
  Devices = 'devices',
  Scrobbling = 'scrobbling',
  ScrobblingHolds = 'scrobble-holds',
  Customize = 'customize',
  CBLImport = 'cbl-import'
}

export enum SettingSectionId {
  AccountSection = 'account-section-title',
  ServerSection = 'server-section-title',
  InsightsSection = 'insights-section-title',
  ImportSection = 'import-section-title',
  InfoSection = 'info-section-title',
  KavitaPlusSection = 'kavitaplus-section-title',
}

interface PrefSection {
  title: SettingSectionId;
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
  kPlusOnly: boolean;

  constructor(fragment: SettingsTabId, roles: Array<Role> = [], badgeCount$: Observable<number> | undefined = undefined, restrictRoles: Array<Role> = [], kPlusOnly: boolean = false) {
    this.fragment = fragment;
    this.roles = roles;
    this.restrictRoles = restrictRoles;
    this.badgeCount$ = badgeCount$;
    this.kPlusOnly = kPlusOnly;
  }

  /**
   * Create a new SideNavItem with kPlusOnly set to true
   * @param fragment
   * @param roles
   * @param badgeCount$
   * @param restrictRoles
   */
  static kPlusOnly(fragment: SettingsTabId, roles: Array<Role> = [], badgeCount$: Observable<number> | undefined = undefined, restrictRoles: Array<Role> = []) {
    return new SideNavItem(fragment, roles, badgeCount$, restrictRoles, true);
  }

}

@Component({
    selector: 'app-preference-nav',
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
  private readonly keyBindService = inject(KeyBindService);

  /**
   * This links to settings.component.html which has triggers on what underlying component to render out.
   */
  sections: Array<PrefSection> = [];

  collapseSideNavOnMobileNav$ = this.router.events.pipe(
    filter(event => event instanceof NavigationEnd),
    takeUntilDestroyed(this.destroyRef),
    map(evt => evt as NavigationEnd),
    switchMap(_ => this.utilityService.activeBreakpoint$),
    filter((b) => b < Breakpoint.Tablet),
    switchMap(() => this.navService.sideNavCollapsed$),
    take(1),
    filter(collapsed => !collapsed),
    tap(_ => {
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
          libraryType: -1,
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

  private readonly mediaIssuesBadgeCount$ = this.accountService.currentUser$.pipe(
    take(1),
    switchMap(user => {
      if (!user || !this.accountService.hasAdminRole(user)) {
        // If no user or user does not have the admin role, return an observable of -1
        return of(-1);
      }

      return this.serverService.getMediaErrors().pipe(
        takeUntilDestroyed(this.destroyRef),
        map(d => d.length),
        shareReplay({ bufferSize: 1, refCount: true })
      );
    })
  );

  constructor() {
    this.collapseSideNavOnMobileNav$.subscribe();

    // Ensure that on mobile, we are collapsed by default
    if (this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet) {
      this.navService.collapseSideNav(true);
    }

    this.sections = [
      {
        title: SettingSectionId.AccountSection,
        children: [
          new SideNavItem(SettingsTabId.Account),
          new SideNavItem(SettingsTabId.Preferences),
          new SideNavItem(SettingsTabId.CustomKeyBinds),
          new SideNavItem(SettingsTabId.ReadingProfiles),
          new SideNavItem(SettingsTabId.Customize, [], undefined, [Role.ReadOnly]),
          new SideNavItem(SettingsTabId.Clients),
          new SideNavItem(SettingsTabId.Theme),
          new SideNavItem(SettingsTabId.Font),
          new SideNavItem(SettingsTabId.Devices),
        ]
      },
      {
        title: SettingSectionId.InsightsSection,
        children: [
          new SideNavItem(SettingsTabId.Activity, [Role.Admin]),
          new SideNavItem(SettingsTabId.AdminDevices, [Role.Admin]),
          new SideNavItem(SettingsTabId.Statistics, [Role.Admin]),
        ]
      },
      {
        title: SettingSectionId.ServerSection,
        children: [
          new SideNavItem(SettingsTabId.General, [Role.Admin]),
          new SideNavItem(SettingsTabId.ManageMetadata, [Role.Admin]),
          new SideNavItem(SettingsTabId.OpenIDConnect, [Role.Admin]),
          new SideNavItem(SettingsTabId.Media, [Role.Admin]),
          new SideNavItem(SettingsTabId.Email, [Role.Admin]),
          new SideNavItem(SettingsTabId.Users, [Role.Admin]),
          new SideNavItem(SettingsTabId.Libraries, [Role.Admin]),
          new SideNavItem(SettingsTabId.Tasks, [Role.Admin]),
        ]
      },
      {
        title: SettingSectionId.ImportSection,
        children: [
          new SideNavItem(SettingsTabId.MappingsImport, [Role.Admin]),
          new SideNavItem(SettingsTabId.CBLImport, [], undefined, [Role.ReadOnly]),
          SideNavItem.kPlusOnly(SettingsTabId.MALStackImport),
        ]
      },
      {
        title: SettingSectionId.InfoSection,
        children: [
          new SideNavItem(SettingsTabId.System, [Role.Admin]),
          new SideNavItem(SettingsTabId.MediaIssues, [Role.Admin], this.mediaIssuesBadgeCount$),
          new SideNavItem(SettingsTabId.EmailHistory, [Role.Admin]),
        ]
      },
      {
        title: SettingSectionId.KavitaPlusSection,
        children: [
          new SideNavItem(SettingsTabId.KavitaPlusLicense, [Role.Admin]),
          SideNavItem.kPlusOnly(SettingsTabId.ManageUserTokens, [Role.Admin]),
          SideNavItem.kPlusOnly(SettingsTabId.Metadata, [Role.Admin]),
          SideNavItem.kPlusOnly(SettingsTabId.MatchedMetadata, [Role.Admin], this.matchedMetadataBadgeCount$),
          SideNavItem.kPlusOnly(SettingsTabId.ScrobblingHolds),
          SideNavItem.kPlusOnly(SettingsTabId.Scrobbling, [], this.scrobblingErrorBadgeCount$),
        ]
      }
    ];

    this.scrollToActiveItem();
    this.cdRef.markForCheck();

    // Refresh visibility if license changes
    effect(() => {
      this.licenseService.hasValidLicenseSignal();
      this.cdRef.markForCheck();
    });

    this.keyBindService.registerListener(
      this.destroyRef,
      () => this.router.navigate(['/settings'], { fragment: SettingsTabId.Scrobbling})
        .then(() => this.scrollToActiveItem()),
      [KeyBindTarget.NavigateToScrobbling],
      {condition$: this.licenseService.hasValidLicense$},
    );
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

  getVisibleChildren(user: User, section: PrefSection) {
    return section.children.filter(item => this.isItemVisible(user, item));
  }

  isItemVisible(user: User, item: SideNavItem) {
    return this.accountService.hasAnyRole(user, item.roles, item.restrictRoles) && (!item.kPlusOnly || this.licenseService.hasValidLicenseSignal())
  }

  collapse() {
    this.navService.toggleSideNav();
  }

  protected readonly Breakpoint = Breakpoint;
}
