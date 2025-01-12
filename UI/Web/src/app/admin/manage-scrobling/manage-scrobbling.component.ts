import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {ManageScrobbleErrorsComponent} from "../manage-scrobble-errors/manage-scrobble-errors.component";
import {AsyncPipe} from "@angular/common";
import {AccountService} from "../../_services/account.service";
import {ScrobblingHoldsComponent} from "../../user-settings/user-holds/scrobbling-holds.component";
import {
  UserScrobbleHistoryComponent
} from "../../_single-module/user-scrobble-history/user-scrobble-history.component";

@Component({
  selector: 'app-manage-scrobling',
  standalone: true,
  imports: [
    ManageScrobbleErrorsComponent,
    AsyncPipe,
    UserScrobbleHistoryComponent
  ],
  templateUrl: './manage-scrobbling.component.html',
  styleUrl: './manage-scrobbling.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageScrobblingComponent {

  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);

  scrobbleCount: number = 0;

  updateScrobbleErrorCount(count: number) {
    this.scrobbleCount = count;
    this.cdRef.markForCheck();
  }
}
