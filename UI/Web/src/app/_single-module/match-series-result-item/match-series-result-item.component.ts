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

@Component({
  selector: 'app-match-series-result-item',
  standalone: true,
  imports: [
    ImageComponent,
    TranslocoPercentPipe,
    ReadMoreComponent,
    TranslocoDirective,
    PlusMediaFormatPipe
  ],
  templateUrl: './match-series-result-item.component.html',
  styleUrl: './match-series-result-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatchSeriesResultItemComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) item!: ExternalSeriesMatch;
  @Output() selected: EventEmitter<ExternalSeriesMatch> = new EventEmitter();

  selectItem() {
    this.selected.emit(this.item);
  }

}
