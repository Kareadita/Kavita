import {ChangeDetectionStrategy, Component, ElementRef, inject, input, viewChild} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {DrawerService} from "../../_services/drawer.service";

@Component({
  selector: 'app-external-series-card',
  imports: [ImageComponent, NgbTooltip, ReactiveFormsModule, TranslocoDirective, NgOptimizedImage, ProviderImagePipe],
  templateUrl: './external-series-card.component.html',
  styleUrls: ['./external-series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalSeriesCardComponent {
  private readonly drawerService = inject(DrawerService);

  data = input.required<ExternalSeries>();
  /**
   * When clicking on the series, instead of opening, opens a preview drawer
   */
  previewOnClick = input<boolean>(false);
  link = viewChild<ElementRef<HTMLAnchorElement>>('link')


  handleClick() {
    if (this.previewOnClick()) {
      const ref = this.drawerService.open(SeriesPreviewDrawerComponent, {position: 'end', panelClass: ''});

      ref.setInput('isExternalSeries', true);
      ref.setInput('aniListId', this.data().aniListId);
      ref.setInput('malId', this.data().malId);
      ref.setInput('name', this.data().name);
      return;
    }
    const linkElem = this.link()?.nativeElement;
    if (linkElem) {
      linkElem.click();
    }
  }
}
