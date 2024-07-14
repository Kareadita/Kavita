import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {User} from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import {ActivatedRoute, RouterLink} from '@angular/router';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {UserHoldsComponent} from '../user-holds/user-holds.component';
import {UserScrobbleHistoryComponent} from '../../_single-module/user-scrobble-history/user-scrobble-history.component';
import {UserStatsComponent} from '../../statistics/_components/user-stats/user-stats.component';
import {ManageDevicesComponent} from '../manage-devices/manage-devices.component';
import {ThemeManagerComponent} from '../theme-manager/theme-manager.component';
import {ChangeAgeRestrictionComponent} from '../change-age-restriction/change-age-restriction.component';
import {ChangePasswordComponent} from '../change-password/change-password.component';
import {ChangeEmailComponent} from '../change-email/change-email.component';
import {
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLinkBase,
  NgbNavOutlet,
} from '@ng-bootstrap/ng-bootstrap';
import {
  SideNavCompanionBarComponent
} from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@ngneat/transloco";
import {ManageScrobblingProvidersComponent} from "../manage-scrobbling-providers/manage-scrobbling-providers.component";
import {
  ManageUserPreferencesComponent
} from "../manga-user-preferences/manage-user-preferences.component";
import {ManageOpdsComponent} from "../manage-opds/manage-opds.component";

enum FragmentID {
  Account = 'account',
  Preferences = '',
  Clients = 'clients',
  Theme = 'theme',
  Devices = 'devices',
  Stats = 'stats',
  Scrobbling = 'scrobbling'
}

@Component({
  selector: 'app-user-preferences',
  templateUrl: './user-preferences.component.html',
  styleUrls: ['./user-preferences.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [
    NgbNav,
    TranslocoDirective,
    SideNavCompanionBarComponent,
    NgbNavLinkBase,
    NgbNavContent,
    RouterLink,
    ChangeEmailComponent,
    ChangePasswordComponent,
    ChangeAgeRestrictionComponent,
    ManageScrobblingProvidersComponent,
    ManageUserPreferencesComponent,
    ManageOpdsComponent,
    ThemeManagerComponent,
    ManageDevicesComponent,
    UserStatsComponent,
    UserScrobbleHistoryComponent,
    UserHoldsComponent,
    NgbNavOutlet,
    NgbNavItem
  ],
})
export class UserPreferencesComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly titleService = inject(Title);
  private readonly route = inject(ActivatedRoute);
  private readonly cdRef = inject(ChangeDetectorRef);

  protected readonly FragmentID = FragmentID;

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'account-tab', fragment: FragmentID.Account},
    {title: 'preferences-tab', fragment: FragmentID.Preferences},
    {title: '3rd-party-clients-tab', fragment: FragmentID.Clients},
    {title: 'theme-tab', fragment: FragmentID.Theme},
    {title: 'devices-tab', fragment: FragmentID.Devices},
    {title: 'stats-tab', fragment: FragmentID.Stats},
  ];
  active = this.tabs[1];
  hasActiveLicense = false;


  constructor() {
    this.accountService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res) {
        if (this.tabs.filter(t => t.fragment == FragmentID.Scrobbling).length === 0) {
          this.tabs.push({title: 'scrobbling-tab', fragment: FragmentID.Scrobbling});
        }

        this.hasActiveLicense = true;
        this.cdRef.markForCheck();
      }

      this.route.fragment.subscribe(frag => {
        const tab = this.tabs.filter(item => item.fragment === frag);
        if (tab.length > 0) {
          this.active = tab[0];
        } else {
          this.active = this.tabs[1]; // Default to preferences
        }
        this.cdRef.markForCheck();
      });
    });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - User Preferences');
  }
}
