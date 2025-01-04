import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {ApiKeyComponent} from "../api-key/api-key.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {SettingsService} from "../../admin/settings.service";
import {User} from "../../_models/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {WikiLink} from "../../_models/wiki";
import {LicenseService} from "../../_services/license.service";

@Component({
  selector: 'app-manage-opds',
  standalone: true,
  imports: [
    ApiKeyComponent,
    TranslocoDirective,
    SettingTitleComponent,
    SettingItemComponent
  ],
  templateUrl: './manage-opds.component.html',
  styleUrl: './manage-opds.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageOpdsComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly settingsService = inject(SettingsService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly licenseService = inject(LicenseService);


  user: User | undefined = undefined;
  opdsUrlLink = `<a href="${WikiLink.OpdsClients}" target="_blank" rel="noopener noreferrer">Wiki</a>`

  opdsEnabled: boolean = false;
  opdsUrl: string = '';
  hasActiveLicense = false;
  makeUrl: (val: string) => string = (val: string) => { return this.opdsUrl; };

  constructor() {
    this.accountService.getOpdsUrl().subscribe(res => {
      this.opdsUrl = res;
      this.cdRef.markForCheck();
    });

    this.settingsService.getOpdsEnabled().subscribe(res => {
      this.opdsEnabled = res;
      this.cdRef.markForCheck();
    });

    this.licenseService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      this.hasActiveLicense = res;
      this.cdRef.markForCheck();
    });
  }

}
