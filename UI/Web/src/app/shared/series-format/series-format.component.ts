import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { MangaFormat } from 'src/app/_models/manga-format';

@Component({
  selector: 'app-series-format',
  templateUrl: './series-format.component.html',
  styleUrls: ['./series-format.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesFormatComponent {

  @Input() format: MangaFormat = MangaFormat.UNKNOWN;

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }
}
