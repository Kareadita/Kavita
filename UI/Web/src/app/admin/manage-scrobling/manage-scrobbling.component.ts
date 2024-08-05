import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {ManageScrobbleErrorsComponent} from "../manage-scrobble-errors/manage-scrobble-errors.component";
import {AsyncPipe} from "@angular/common";
import {AccountService} from "../../_services/account.service";
import {map, shareReplay} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
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
    ScrobblingHoldsComponent,
    UserScrobbleHistoryComponent
  ],
  templateUrl: './manage-scrobbling.component.html',
  styleUrl: './manage-scrobbling.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageScrobblingComponent {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);

  isAdmin$ = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef),
    map(user => (user && this.accountService.hasAdminRole(user)) || false),
    shareReplay({bufferSize: 1, refCount: true})
  );

  scrobbleCount: number = 0;

  updateScrobbleErrorCount(count: number) {
    this.scrobbleCount = count;
    this.cdRef.markForCheck();
  }
}
