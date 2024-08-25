import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveOffcanvas, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ExternalSeriesDetail, SeriesStaff} from "../../_models/series-detail/external-series-detail";
import {SeriesService} from "../../_services/series.service";
import {ImageComponent} from "../../shared/image/image.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {A11yClickDirective} from "../../shared/a11y-click.directive";
import {MetadataDetailComponent} from "../../series-detail/_components/metadata-detail/metadata-detail.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {ImageService} from "../../_services/image.service";
import {PublicationStatusPipe} from "../../_pipes/publication-status.pipe";
import {SeriesMetadata} from "../../_models/metadata/series-metadata";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {ActionService} from "../../_services/action.service";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {FilterField} from "../../_models/metadata/v2/filter-field";

@Component({
  selector: 'app-series-preview-drawer',
  standalone: true,
    imports: [TranslocoDirective, ImageComponent, LoadingComponent, SafeHtmlPipe, A11yClickDirective, MetadataDetailComponent, PersonBadgeComponent, TagBadgeComponent, PublicationStatusPipe, ReadMoreComponent, NgbTooltip, NgOptimizedImage, ProviderImagePipe],
  templateUrl: './series-preview-drawer.component.html',
  styleUrls: ['./series-preview-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesPreviewDrawerComponent implements OnInit {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly seriesService = inject(SeriesService);
  private readonly imageService = inject(ImageService);
  private readonly actionService = inject(ActionService);
  private readonly cdRef = inject(ChangeDetectorRef);

  protected readonly FilterField = FilterField;

  @Input({required: true}) name!: string;
  @Input() aniListId?: number;
  @Input() malId?: number;
  @Input() seriesId?: number;
  @Input() libraryId: number = 0;
  @Input({required: true}) isExternalSeries: boolean = true;

  isLoading: boolean = true;
  localStaff: Array<SeriesStaff> = [];
  externalSeries: ExternalSeriesDetail | undefined;
  localSeries: SeriesMetadata | undefined;
  url: string = '';
  wantToRead: boolean = false;



  get CoverUrl() {
    if (this.isExternalSeries) {
      if (this.externalSeries) return this.externalSeries.coverUrl;
      return this.imageService.placeholderImage;
    }
    return this.imageService.getSeriesCoverImage(this.seriesId!);
  }


  ngOnInit() {
    if (this.isExternalSeries) {
      this.seriesService.getExternalSeriesDetails(this.aniListId, this.malId).subscribe(externalSeries => {
        this.externalSeries = externalSeries;
        this.isLoading = false;
        if (this.externalSeries.siteUrl) {
          this.url = this.externalSeries.siteUrl;
        }

        this.cdRef.markForCheck();
      });
    } else {
      this.seriesService.getMetadata(this.seriesId!).subscribe(data => {
        this.localSeries = data;

        // Consider the localSeries has no metadata, try to merge the external Series metadata
        if (this.localSeries.summary === '' && this.localSeries.genres.length === 0) {
          this.seriesService.getExternalSeriesDetails(0, 0, this.seriesId).subscribe(externalSeriesData => {
            this.isExternalSeries = true;
            this.externalSeries = externalSeriesData;
            this.cdRef.markForCheck();
          })
        }

        this.seriesService.isWantToRead(this.seriesId!).subscribe(wantToRead => {
          this.wantToRead = wantToRead;
          this.cdRef.markForCheck();
        });

        this.isLoading = false;
        this.url = 'library/' + this.libraryId + '/series/' + this.seriesId;
        this.localStaff = data.writers.map(p => {
          return {name: p.name, role: 'Story & Art'} as SeriesStaff;
        });
        this.cdRef.markForCheck();
      });
    }

  }

  toggleWantToRead() {
    if (this.wantToRead) {
      this.actionService.removeMultipleSeriesFromWantToReadList([this.seriesId!]);
    } else {
      this.actionService.addMultipleSeriesToWantToReadList([this.seriesId!]);
    }

    this.wantToRead = !this.wantToRead;
    this.cdRef.markForCheck();
  }

  close() {
    this.activeOffcanvas.close();
  }
}
