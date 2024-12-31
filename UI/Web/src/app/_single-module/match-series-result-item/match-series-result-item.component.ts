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
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {ExternalSeriesDetail} from "../../_models/series-detail/external-series-detail";

@Component({
  selector: 'app-match-series-result-item',
  standalone: true,
  imports: [
    ImageComponent,
    SeriesFormatComponent
  ],
  templateUrl: './match-series-result-item.component.html',
  styleUrl: './match-series-result-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatchSeriesResultItemComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) item!: ExternalSeriesDetail;
  @Output() selected: EventEmitter<ExternalSeriesDetail> = new EventEmitter();

  selectItem() {
    this.selected.emit(this.item);
  }

}
