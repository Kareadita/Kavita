import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {MangaFormat} from "../../_models/manga-format";
import {MangaFormatPipe} from "../../_pipes/manga-format.pipe";
import {MangaFormatIconPipe} from "../../_pipes/manga-format-icon.pipe";

function formatColorVar(format: MangaFormat): string {
  switch (format) {
    case MangaFormat.EPUB:
      return '--format-badge-epub-color';
    case MangaFormat.ARCHIVE:
      return '--format-badge-archive-color';
    case MangaFormat.IMAGE:
      return '--format-badge-image-color';
    case MangaFormat.PDF:
      return '--format-badge-pdf-color';
    case MangaFormat.UNKNOWN:
      return '--format-badge-unknown-color';
  }
}

/**
 * Renders the format as a labelled pill. Prefer this over app-series-format where there is room for
 * the name, since a bare icon carries no meaning on its own.
 */
@Component({
  selector: 'app-format-badge',
  imports: [MangaFormatPipe, MangaFormatIconPipe],
  templateUrl: './format-badge.component.html',
  styleUrl: './format-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormatBadgeComponent {
  protected readonly MangaFormat = MangaFormat;

  format = input<MangaFormat>(MangaFormat.UNKNOWN);

  protected readonly colorVar = computed(() => `var(${formatColorVar(this.format())})`);
}
