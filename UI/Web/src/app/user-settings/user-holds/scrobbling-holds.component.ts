import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {ScrobblingService} from "../../_services/scrobbling.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageService} from "../../_services/image.service";
import {ImageComponent} from "../../shared/image/image.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {ScrobbleHold} from "../../_models/scrobbling/scrobble-hold";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {APP_BASE_HREF} from "@angular/common";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";

@Component({
    selector: 'app-user-holds',
  imports: [TranslocoDirective, ImageComponent, UtcToLocalTimePipe, NgxDatatableModule, ResponsiveTableComponent],
    templateUrl: './scrobbling-holds.component.html',
    styleUrls: ['./scrobbling-holds.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScrobblingHoldsComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrobblingService = inject(ScrobblingService);
  protected readonly imageService = inject(ImageService);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  isLoading = true;
  data: Array<ScrobbleHold> = [];
  trackBy = (idx: number, item: ScrobbleHold) => `${item.seriesId}_${idx}`;

  constructor() {
    this.loadData();
  }

  loadData() {
    this.scrobblingService.getHolds().subscribe(data => {
      this.data = data;
      this.isLoading = false;
      this.cdRef.markForCheck();
    })
  }

  removeHold(hold: ScrobbleHold) {
    this.scrobblingService.removeHold(hold.seriesId).subscribe(_ => {
      this.loadData();
    });
  }
}
