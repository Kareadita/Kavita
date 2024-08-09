import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {AsyncPipe} from "@angular/common";
import {
  KavitaplusMetadataBreakdownStatsComponent
} from "../../statistics/_components/kavitaplus-metadata-breakdown-stats/kavitaplus-metadata-breakdown-stats.component";
import {LicenseComponent} from "../license/license.component";
import {AccountService} from "../../_services/account.service";

@Component({
  selector: 'app-manage-kavitaplus',
  standalone: true,
  imports: [
    AsyncPipe,
    KavitaplusMetadataBreakdownStatsComponent,
    LicenseComponent
  ],
  templateUrl: './manage-kavitaplus.component.html',
  styleUrl: './manage-kavitaplus.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageKavitaplusComponent {
  protected readonly accountService = inject(AccountService);
}
