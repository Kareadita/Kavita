import {ChangeDetectionStrategy, Component, computed, ElementRef, inject, input, viewChild} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {RecommendationSourcePipe} from "../../_pipes/recommendation-source.pipe";
import {DrawerService} from "../../_services/drawer.service";
import {MetadataProvider} from "../../_models/kavitaplus/metadata-provider.enum";
import {ScrobbleProvider} from "../../_services/scrobbling.service";

@Component({
  selector: 'app-external-series-card',
  imports: [ImageComponent, NgbTooltip, ReactiveFormsModule, TranslocoDirective, NgOptimizedImage, ProviderImagePipe, RecommendationSourcePipe],
  templateUrl: './external-series-card.component.html',
  styleUrls: ['./external-series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalSeriesCardComponent {
  private readonly drawerService = inject(DrawerService);

  seriesId = input.required<number>()
  data = input.required<ExternalSeries>();

  protected readonly scrobbleProvider = computed(() => {
    switch (this.data().metadataProvider) {
      case MetadataProvider.Hardcover:
        return ScrobbleProvider.Hardcover;
      case MetadataProvider.Mangabaka:
        return ScrobbleProvider.MangaBaka;
      case MetadataProvider.ComicBookRoundup:
        return ScrobbleProvider.Cbr;
      default:
        return ScrobbleProvider.MangaBaka;
    }
  });
  /**
   * When clicking on the series, instead of opening, opens a preview drawer
   */
  previewOnClick = input<boolean>(false);
  link = viewChild<ElementRef<HTMLAnchorElement>>('link')


  handleClick() {
    if (this.previewOnClick()) {
      const ref = this.drawerService.open(SeriesPreviewDrawerComponent, {position: 'end', panelClass: ''});

      ref.setInput('isExternalSeries', true);
      ref.setInput('seriesId', this.seriesId());
      ref.setInput('mangaBakaId', this.data().mangaBakaId)
      ref.setInput('aniListId', this.data().aniListId);
      ref.setInput('malId', this.data().malId);
      ref.setInput('hardcoverId', this.data().hardcoverId)
      ref.setInput('name', this.data().name);
      return;
    }
    const linkElem = this.link()?.nativeElement;
    if (linkElem) {
      linkElem.click();
    }
  }
}
