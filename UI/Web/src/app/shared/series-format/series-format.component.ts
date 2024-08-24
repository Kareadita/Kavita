import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { MangaFormat } from 'src/app/_models/manga-format';
import {MangaFormatIconPipe} from "../../_pipes/manga-format-icon.pipe";
import {MangaFormatPipe} from "../../_pipes/manga-format.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-series-format',
  standalone: true,
  imports: [
    MangaFormatIconPipe,
    MangaFormatPipe,
    NgbTooltip
  ],
  templateUrl: './series-format.component.html',
  styleUrls: ['./series-format.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesFormatComponent {

  protected readonly MangaFormat = MangaFormat;

  @Input() format: MangaFormat = MangaFormat.UNKNOWN;
  /**
   * Use the browser title vs ngbTooltip
   */
  @Input() useTitle: boolean = true;
}
