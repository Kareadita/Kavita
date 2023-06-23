import {ChangeDetectionStrategy, Component, DestroyRef, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {PipeModule} from "../../pipe/pipe.module";
import {TableModule} from "../table/table.module";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {shareReplay} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ScrobbleEventTypePipe} from "../scrobble-event-type.pipe";

@Component({
  selector: 'app-user-scrobble-history',
  standalone: true,
  imports: [CommonModule, PipeModule, TableModule, ScrobbleEventTypePipe],
  templateUrl: './user-scrobble-history.component.html',
  styleUrls: ['./user-scrobble-history.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserScrobbleHistoryComponent {

  private readonly scrobbleService = inject(ScrobblingService);
  private readonly destroyRef = inject(DestroyRef);
  events$ = this.scrobbleService.getScrobbleEvents().pipe(shareReplay(), takeUntilDestroyed(this.destroyRef));

  get ScrobbleEventType() { return ScrobbleEventType; }
}
