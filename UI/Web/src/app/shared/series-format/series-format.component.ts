import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { MangaFormat } from 'src/app/_models/manga-format';
import {MangaFormatIconPipe} from "../../pipe/manga-format-icon.pipe";
import {MangaFormatPipe} from "../../pipe/manga-format.pipe";
import {CommonModule} from "@angular/common";

@Component({
  selector: 'app-series-format',
  standalone: true,
  imports: [
    CommonModule,
    MangaFormatIconPipe,
    MangaFormatPipe
  ],
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
