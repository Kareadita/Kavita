import {
  ChangeDetectionStrategy,
  Component,
  ElementRef, inject,
  Input,
  ViewChild
} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {RouterLinkActive} from "@angular/router";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbOffcanvas, NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-external-series-card',
  standalone: true,
  imports: [ImageComponent, NgbProgressbar, NgbTooltip, ReactiveFormsModule, RouterLinkActive, TranslocoDirective, NgOptimizedImage, ProviderImagePipe, SafeHtmlPipe],
  templateUrl: './external-series-card.component.html',
  styleUrls: ['./external-series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalSeriesCardComponent {
  private readonly offcanvasService = inject(NgbOffcanvas);

  @Input({required: true}) data!: ExternalSeries;
  /**
   * When clicking on the series, instead of opening, opens a preview drawer
   */
  @Input() previewOnClick: boolean = false;
  @ViewChild('link', {static: false}) link!: ElementRef<HTMLAnchorElement>;


  handleClick() {
    if (this.previewOnClick) {
      const ref = this.offcanvasService.open(SeriesPreviewDrawerComponent, {position: 'end', panelClass: ''});
      ref.componentInstance.isExternalSeries = true;
      ref.componentInstance.aniListId = this.data.aniListId;
      ref.componentInstance.malId = this.data.malId;
      ref.componentInstance.name = this.data.name;
      return;
    }
    if (this.link) {
      this.link.nativeElement.click();
    }
  }
}
