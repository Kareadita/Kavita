import {
  ChangeDetectionStrategy,
  Component,
  ElementRef, inject,
  Input,
  ViewChild
} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {RouterLinkActive} from "@angular/router";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbActiveOffcanvas, NgbOffcanvas, NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@ngneat/transloco";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {SeriesService} from "../../_services/series.service";

@Component({
  selector: 'app-external-series-card',
  standalone: true,
  imports: [CommonModule, ImageComponent, NgbProgressbar, NgbTooltip, ReactiveFormsModule, RouterLinkActive, TranslocoDirective],
  templateUrl: './external-series-card.component.html',
  styleUrls: ['./external-series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalSeriesCardComponent {
  @Input({required: true}) data!: ExternalSeries;
  /**
   * When clicking on the series, instead of opening, opens a preview drawer
   */
  @Input() previewOnClick: boolean = false;
  @ViewChild('link', {static: false}) link!: ElementRef<HTMLAnchorElement>;

  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly seriesService = inject(SeriesService);

  handleClick() {
    if (this.previewOnClick) {
      const ref = this.offcanvasService.open(SeriesPreviewDrawerComponent, {position: 'end', panelClass: 'navbar-offset'});
      ref.componentInstance.isExternal = true;
      ref.componentInstance.aniListId = this.data.aniListId;
      ref.componentInstance.malId = this.data.malId;
      return;
    }
    if (this.link) {
      this.link.nativeElement.click();
    }
  }
}
