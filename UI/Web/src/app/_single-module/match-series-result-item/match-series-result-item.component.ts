import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  Output
} from '@angular/core';
import {ImageComponent} from "../../shared/image/image.component";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {ExternalSeriesMatch} from "../../_models/series-detail/external-series-match";
import {PercentPipe} from "@angular/common";
import {TranslocoPercentPipe} from "@jsverse/transloco-locale";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {PlusMediaFormatPipe} from "../../_pipes/plus-media-format.pipe";
import {LoadingComponent} from "../../shared/loading/loading.component";

@Component({
  selector: 'app-match-series-result-item',
  standalone: true,
  imports: [
    ImageComponent,
    TranslocoPercentPipe,
    ReadMoreComponent,
    TranslocoDirective,
    PlusMediaFormatPipe,
    LoadingComponent
  ],
  templateUrl: './match-series-result-item.component.html',
  styleUrl: './match-series-result-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatchSeriesResultItemComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) item!: ExternalSeriesMatch;
  @Input({required: true}) isDarkMode = true;
  @Output() selected: EventEmitter<ExternalSeriesMatch> = new EventEmitter();

  isSelected = false;

  selectItem() {
    if (this.isSelected) return;

    this.isSelected = true;
    this.cdRef.markForCheck();
    this.selected.emit(this.item);
  }

}
