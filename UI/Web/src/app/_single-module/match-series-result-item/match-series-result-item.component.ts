import {ChangeDetectionStrategy, Component, input, output, signal} from '@angular/core';
import {ImageComponent} from "../../shared/image/image.component";
import {ExternalSeriesMatch} from "../../_models/series-detail/external-series-match";
import {TranslocoPercentPipe} from "@jsverse/transloco-locale";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {PlusMediaFormatPipe} from "../../_pipes/plus-media-format.pipe";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {PlusMediaFormat} from "../../_models/series-detail/external-series-detail";

@Component({
  selector: 'app-match-series-result-item',
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

  item = input.required<ExternalSeriesMatch>();
  isDarkMode = input(true);
  selected = output<ExternalSeriesMatch>();

  isSelected = signal<boolean>(false);

  selectItem() {
    if (this.isSelected()) return;

    this.isSelected.set(true);

    this.selected.emit(this.item());
  }

  protected readonly PlusMediaFormat = PlusMediaFormat;
}
