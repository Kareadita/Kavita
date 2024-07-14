import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {ApiKeyComponent} from "../api-key/api-key.component";
import {NgIf} from "@angular/common";
import {TranslocoDirective} from "@ngneat/transloco";
import {AccountService} from "../../_services/account.service";
import {SettingsService} from "../../admin/settings.service";
import {User} from "../../_models/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-manage-opds',
  standalone: true,
  imports: [
    ApiKeyComponent,
    NgIf,
    TranslocoDirective
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

  user: User | undefined = undefined;

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

    this.accountService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      this.hasActiveLicense = res;
      this.cdRef.markForCheck();
    });
  }
}
