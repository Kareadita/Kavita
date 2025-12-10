import {ChangeDetectionStrategy, Component, EventEmitter, inject, input, Output} from '@angular/core';
import {DecimalPipe, DOCUMENT} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {IHasProgress} from "../../_models/common/i-has-progress";

/**
 * Used for the Series/Volume/Chapter Detail pages
 */
@Component({
  selector: 'app-cover-image',
  imports: [
      TranslocoDirective,
      ImageComponent,
      NgbProgressbar,
      DecimalPipe,
      NgbTooltip
  ],
  templateUrl: './cover-image.component.html',
  styleUrl: './cover-image.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CoverImageComponent {

  private readonly document = inject(DOCUMENT);

  coverImage = input.required<string>();
  entity = input.required<IHasProgress>();
  continueTitle = input<string>('');
  @Output() read = new EventEmitter();

  mobileSeriesImgBackground = getComputedStyle(this.document.documentElement)
    .getPropertyValue('--mobile-series-img-background').trim();

}
