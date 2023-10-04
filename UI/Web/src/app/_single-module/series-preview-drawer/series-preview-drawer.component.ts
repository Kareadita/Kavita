import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {ExternalSeriesDetail} from "../../_models/series-detail/external-series-detail";
import {SeriesService} from "../../_services/series.service";

@Component({
  selector: 'app-series-preview-drawer',
  standalone: true,
  imports: [CommonModule, TranslocoDirective],
  templateUrl: './series-preview-drawer.component.html',
  styleUrls: ['./series-preview-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesPreviewDrawerComponent implements OnInit {

  externalSeries: ExternalSeriesDetail | undefined;
  @Input() aniListId?: number;
  @Input() malId?: number;
  @Input({required: true}) isExternalSeries: boolean = true;

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly seriesService = inject(SeriesService);
  private readonly cdRef = inject(ChangeDetectorRef);

  ngOnInit() {
    this.seriesService.getExternalSeriesDetails(this.aniListId, this.malId).subscribe(externalSeries => {
      this.externalSeries = externalSeries;
      console.log('External Series Detail: ', this.externalSeries);
      this.cdRef.markForCheck();
    });
  }

  close() {
    this.activeOffcanvas.close();
  }
}
