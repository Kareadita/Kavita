import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {ManageScrobbleErrorsComponent} from "../manage-scrobble-errors/manage-scrobble-errors.component";
import {RouterLink} from "@angular/router";
import {AccountService} from "../../../_services/account.service";
import {
  UserScrobbleHistoryComponent
} from "../../../_single-module/user-scrobble-history/user-scrobble-history.component";

@Component({
    selector: 'app-manage-scrobling',
  imports: [
    ManageScrobbleErrorsComponent,
    UserScrobbleHistoryComponent,
    RouterLink
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
