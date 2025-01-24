import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {
  ChangeAgeRestrictionComponent
} from "../../../user-settings/change-age-restriction/change-age-restriction.component";
import {ChangeEmailComponent} from "../../../user-settings/change-email/change-email.component";
import {ChangePasswordComponent} from "../../../user-settings/change-password/change-password.component";
import {ManageDevicesComponent} from "../../../user-settings/manage-devices/manage-devices.component";
import {ManageOpdsComponent} from "../../../user-settings/manage-opds/manage-opds.component";
import {
  ManageScrobblingProvidersComponent
} from "../../../user-settings/manage-scrobbling-providers/manage-scrobbling-providers.component";
import {
  ManageUserPreferencesComponent
} from "../../../user-settings/manga-user-preferences/manage-user-preferences.component";
import {ActivatedRoute, Router} from "@angular/router";
import {
  SideNavCompanionBarComponent
} from "../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {ThemeManagerComponent} from "../../../user-settings/theme-manager/theme-manager.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {UserStatsComponent} from "../../../statistics/_components/user-stats/user-stats.component";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {AsyncPipe} from "@angular/common";
import {AccountService} from "../../../_services/account.service";
import {WikiLink} from "../../../_models/wiki";
import {LicenseComponent} from "../../../admin/license/license.component";
import {ManageEmailSettingsComponent} from "../../../admin/manage-email-settings/manage-email-settings.component";
import {ManageLibraryComponent} from "../../../admin/manage-library/manage-library.component";
import {ManageMediaSettingsComponent} from "../../../admin/manage-media-settings/manage-media-settings.component";
import {ManageSettingsComponent} from "../../../admin/manage-settings/manage-settings.component";
import {ManageSystemComponent} from "../../../admin/manage-system/manage-system.component";
import {ManageTasksSettingsComponent} from "../../../admin/manage-tasks-settings/manage-tasks-settings.component";
import {ManageUsersComponent} from "../../../admin/manage-users/manage-users.component";
import {ServerStatsComponent} from "../../../statistics/_components/server-stats/server-stats.component";
import {SettingFragmentPipe} from "../../../_pipes/setting-fragment.pipe";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {tap} from "rxjs";
import {ManageScrobblingComponent} from "../../../admin/manage-scrobling/manage-scrobbling.component";
import {ManageMediaIssuesComponent} from "../../../admin/manage-media-issues/manage-media-issues.component";
import {
  ManageCustomizationComponent
} from "../../../sidenav/_components/manage-customization/manage-customization.component";
import {
  ImportMalCollectionComponent
} from "../../../collections/_components/import-mal-collection/import-mal-collection.component";
import {ImportCblComponent} from "../../../reading-list/_components/import-cbl/import-cbl.component";
import {LicenseService} from "../../../_services/license.service";
import {ManageMatchedMetadataComponent} from "../../../admin/manage-matched-metadata/manage-matched-metadata.component";
import {ManageUserTokensComponent} from "../../../admin/manage-user-tokens/manage-user-tokens.component";
import {EmailHistoryComponent} from "../../../admin/email-history/email-history.component";
import {ScrobblingHoldsComponent} from "../../../user-settings/user-holds/scrobbling-holds.component";
import {
  ManageMetadataSettingsComponent
} from "../../../admin/manage-metadata-settings/manage-metadata-settings.component";

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    ChangeAgeRestrictionComponent,
    ChangeEmailComponent,
    ChangePasswordComponent,
    ManageDevicesComponent,
    ManageOpdsComponent,
    ManageScrobblingProvidersComponent,
    ManageUserPreferencesComponent,
    SideNavCompanionBarComponent,
    ThemeManagerComponent,
    TranslocoDirective,
    UserStatsComponent,
    AsyncPipe,
    LicenseComponent,
    ManageEmailSettingsComponent,
    ManageLibraryComponent,
    ManageMediaSettingsComponent,
    ManageSettingsComponent,
    ManageSystemComponent,
    ManageTasksSettingsComponent,
    ManageUsersComponent,
    ServerStatsComponent,
    SettingFragmentPipe,
    ManageScrobblingComponent,
    ManageMediaIssuesComponent,
    ManageCustomizationComponent,
    ImportMalCollectionComponent,
    ImportCblComponent,
    ManageMatchedMetadataComponent,
    ManageUserTokensComponent,
    EmailHistoryComponent,
    ScrobblingHoldsComponent,
    ManageMetadataSettingsComponent
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingsComponent {

  private readonly route = inject(ActivatedRoute);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);

  protected readonly SettingsTabId = SettingsTabId;
  protected readonly WikiLink = WikiLink;

  fragment: SettingsTabId = SettingsTabId.Account;
  hasActiveLicense = false;

  constructor() {
    this.route.fragment.pipe(tap(frag => {
      if (frag === null) {
        frag = SettingsTabId.Account;
      }
      if (!Object.values(SettingsTabId).includes(frag as SettingsTabId)) {
        this.router.navigate(['home']);
        return;
      }
      this.fragment = frag as SettingsTabId;

      //this.titleService.setTitle('Kavita - ' + translate('admin-dashboard.title'));

      this.cdRef.markForCheck();
    }), takeUntilDestroyed(this.destroyRef)).subscribe();

    this.licenseService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res) {
        this.hasActiveLicense = true;
        this.cdRef.markForCheck();
      }
    });
  }
}
