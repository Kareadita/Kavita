import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {AsyncPipe} from '@angular/common';
import {ScrobblingService} from "../../_services/scrobbling.service";
import {shareReplay} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEventTypePipe} from "../../_pipes/scrobble-event-type.pipe";

import {
  NgbAccordionBody,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageService} from "../../_services/image.service";
import {ImageComponent} from "../../shared/image/image.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {tap} from "rxjs";
import {ScrobbleHold} from "../../_models/scrobbling/scrobble-hold";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";

@Component({
  selector: 'app-user-holds',
  standalone: true,
  imports: [TranslocoDirective, AsyncPipe, ImageComponent, UtcToLocalTimePipe, CardActionablesComponent, DefaultValuePipe, LoadingComponent, NgxDatatableModule],
  templateUrl: './scrobbling-holds.component.html',
  styleUrls: ['./scrobbling-holds.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScrobblingHoldsComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly destroyRef = inject(DestroyRef);
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

  protected readonly ColumnMode = ColumnMode;
}
