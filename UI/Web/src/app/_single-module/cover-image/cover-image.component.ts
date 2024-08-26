import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';
import {DecimalPipe, NgClass} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {IHasProgress} from "../../_models/common/i-has-progress";

/**
 * Used for the Series/Volume/Chapter Detail pages
 */
@Component({
  selector: 'app-cover-image',
  standalone: true,
  imports: [
    NgClass,
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

  @Input({required: true}) coverImage!: string;
  @Input({required: true}) entity!: IHasProgress;
  @Input() continueTitle: string = '';
  @Output() read = new EventEmitter();

  mobileSeriesImgBackground = getComputedStyle(document.documentElement)
    .getPropertyValue('--mobile-series-img-background').trim();

}
