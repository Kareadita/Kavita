import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {ExternalSeriesDetail} from "../../_models/series-detail/external-series-detail";
import {SeriesService} from "../../_services/series.service";
import {ImageComponent} from "../../shared/image/image.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {SafeHtmlPipe} from "../../pipe/safe-html.pipe";
import {A11yClickDirective} from "../../shared/a11y-click.directive";
import {MetadataDetailComponent} from "../../series-detail/_components/metadata-detail/metadata-detail.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";

@Component({
  selector: 'app-series-preview-drawer',
  standalone: true,
  imports: [CommonModule, TranslocoDirective, ImageComponent, LoadingComponent, SafeHtmlPipe, A11yClickDirective, MetadataDetailComponent, PersonBadgeComponent],
  templateUrl: './series-preview-drawer.component.html',
  styleUrls: ['./series-preview-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesPreviewDrawerComponent implements OnInit {

  externalSeries: ExternalSeriesDetail | undefined;
  @Input() aniListId?: number;
  @Input() malId?: number;
  @Input({required: true}) isExternalSeries: boolean = true;

  isLoading: boolean = true;

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly seriesService = inject(SeriesService);
  private readonly cdRef = inject(ChangeDetectorRef);

  ngOnInit() {
    this.seriesService.getExternalSeriesDetails(this.aniListId, this.malId).subscribe(externalSeries => {
      this.externalSeries = externalSeries;
      this.isLoading = false;
      console.log('External Series Detail: ', this.externalSeries);
      this.cdRef.markForCheck();
    });
  }

  close() {
    this.activeOffcanvas.close();
  }
}
