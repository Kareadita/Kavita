import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {ScrobblingService} from "../../_services/scrobbling.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageService} from "../../_services/image.service";
import {ImageComponent} from "../../shared/image/image.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {ScrobbleHold} from "../../_models/scrobbling/scrobble-hold";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";

@Component({
  selector: 'app-user-holds',
  standalone: true,
  imports: [TranslocoDirective, ImageComponent, UtcToLocalTimePipe, NgxDatatableModule],
  templateUrl: './scrobbling-holds.component.html',
  styleUrls: ['./scrobbling-holds.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScrobblingHoldsComponent {
  protected readonly ColumnMode = ColumnMode;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrobblingService = inject(ScrobblingService);
  protected readonly imageService = inject(ImageService);

  isLoading = true;
  data: Array<ScrobbleHold> = [];

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
