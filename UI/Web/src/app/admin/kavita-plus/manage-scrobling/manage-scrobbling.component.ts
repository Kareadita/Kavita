import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {ManageScrobbleErrorsComponent} from "../manage-scrobble-errors/manage-scrobble-errors.component";
import {AccountService} from "../../../_services/account.service";

@Component({
    selector: 'app-manage-scrobling',
  imports: [
    ManageScrobbleErrorsComponent
  ],
    templateUrl: './manage-scrobbling.component.html',
    styleUrl: './manage-scrobbling.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageScrobblingComponent {

  protected readonly accountService = inject(AccountService);

  scrobbleCount = signal<number>(0);

  updateScrobbleErrorCount(count: number) {
    this.scrobbleCount.set(count);
  }
}
