import {ChangeDetectionStrategy, Component, DestroyRef, inject, signal} from '@angular/core';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageService} from "../../_services/image.service";
import {ImageComponent} from "../../shared/image/image.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {ScrobbleHold} from "../../_models/scrobbling/scrobble-hold";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {APP_BASE_HREF} from "@angular/common";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {EmptyStateComponent} from "../../shared/_components/empty-state/empty-state.component";

@Component({
  selector: 'app-user-holds',
  imports: [TranslocoDirective, ImageComponent, UtcToLocalTimePipe, NgxDatatableModule, ResponsiveTableComponent, LoadingComponent, EmptyStateComponent],
  templateUrl: './scrobbling-holds.component.html',
  styleUrls: ['./scrobbling-holds.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScrobblingHoldsComponent {
  private readonly scrobblingService = inject(ScrobblingService);
  protected readonly imageService = inject(ImageService);
  protected readonly baseUrl = inject(APP_BASE_HREF);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly data = signal<Array<ScrobbleHold>>([]);
  trackBy = (idx: number, item: ScrobbleHold) => `${item.seriesId}_${idx}`;

  constructor() {
    this.loadData();
  }

  loadData() {
    this.isLoading.set(true);
    this.scrobblingService.getHolds().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.data.set(data);
      this.isLoading.set(false);
    });
  }

  removeHold(hold: ScrobbleHold) {
    this.scrobblingService.removeHold(hold.seriesId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(_ => {
      this.loadData();
    });
  }
}
