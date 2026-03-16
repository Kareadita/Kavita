import {ChangeDetectionStrategy, Component, computed, inject, input, model, OnInit, signal} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveOffcanvas, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ExternalSeriesDetail, SeriesStaff} from "../../_models/series-detail/external-series-detail";
import {SeriesService} from "../../_services/series.service";
import {ImageComponent} from "../../shared/image/image.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {MetadataDetailComponent} from "../../series-detail/_components/metadata-detail/metadata-detail.component";
import {ImageService} from "../../_services/image.service";
import {PublicationStatusPipe} from "../../_pipes/publication-status.pipe";
import {SeriesMetadata} from "../../_models/metadata/series-metadata";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {ActionService} from "../../_services/action.service";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {FilterField} from "../../_models/metadata/v2/filter-field";

@Component({
    selector: 'app-series-preview-drawer',
    imports: [TranslocoDirective, ImageComponent, LoadingComponent, MetadataDetailComponent,
      PublicationStatusPipe, ReadMoreComponent, NgbTooltip, NgOptimizedImage, ProviderImagePipe],
    templateUrl: './series-preview-drawer.component.html',
    styleUrls: ['./series-preview-drawer.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesPreviewDrawerComponent implements OnInit {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly seriesService = inject(SeriesService);
  private readonly imageService = inject(ImageService);
  private readonly actionService = inject(ActionService);

  protected readonly FilterField = FilterField;

  name = input.required<string>();
  /** Required for non-external series */
  seriesId = input<number>(0);
  /** Required for non-external series */
  libraryId = input<number>(0);
  aniListId = input<number | undefined>(undefined);
  malId = input<number | undefined>(undefined);
  isExternalSeries = model<boolean>(true);


  isLoading = signal<boolean>(true);
  localStaff = signal<SeriesStaff[]>([]);
  externalSeries = signal<ExternalSeriesDetail | undefined>(undefined);
  localSeries = signal<SeriesMetadata | undefined>(undefined);
  viewSeriesUrl = computed(() => {
    const externalSeriesUrl = this.externalSeries()?.siteUrl;
    const libraryId = this.libraryId();
    const seriesId = this.seriesId();

    return externalSeriesUrl ?? 'library/' + libraryId + '/series/' + seriesId;
  });
  wantToRead = signal<boolean>(false);

  coverUrl = computed(() => {
    const isExternal = this.isExternalSeries();
    const seriesId = this.seriesId();
    const externalSeries = this.externalSeries();

    if (isExternal) {
      if (externalSeries) return externalSeries.coverUrl;
      return this.imageService.placeholderImage;
    }

    return this.imageService.getSeriesCoverImage(seriesId!);
  });


  ngOnInit() {
    if (this.isExternalSeries()) {
      this.seriesService.getExternalSeriesDetails(this.aniListId(), this.malId()).subscribe(externalSeries => {
        this.externalSeries.set(externalSeries);
        this.isLoading.set(false);
      });
    } else {
      this.seriesService.getMetadata(this.seriesId()).subscribe(data => {
        this.localSeries.set(data);

        // Consider the localSeries has no metadata, try to merge the external Series metadata
        if (this.localSeries()!.summary === '' && this.localSeries()!.genres.length === 0) {
          this.seriesService.getExternalSeriesDetails(0, 0, this.seriesId()).subscribe(externalSeriesData => {
            this.isExternalSeries.set(true);
            this.externalSeries.set(externalSeriesData);
          })
        }

        this.seriesService.isWantToRead(this.seriesId()).subscribe(wantToRead => {
          this.wantToRead.set(wantToRead);
        });

        this.isLoading.set(false);

        this.localStaff.set(data.writers.map(p => {
          return {name: p.name, role: translate('series-preview-drawer.story-and-art-label')} as SeriesStaff;
        }));
      });
    }
  }

  toggleWantToRead() {
    if (this.wantToRead()) {
      this.actionService.removeMultipleSeriesFromWantToReadList([this.seriesId()]);
    } else {
      this.actionService.addMultipleSeriesToWantToReadList([this.seriesId()]);
    }

    this.wantToRead.update(x => !x);
  }

  close() {
    this.activeOffcanvas.close();
  }
}
