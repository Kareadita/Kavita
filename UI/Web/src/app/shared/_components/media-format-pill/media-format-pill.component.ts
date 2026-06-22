import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {PlusMediaFormat} from "../../../_models/series-detail/external-series-detail";
import {PlusMediaFormatPipe} from "../../../_pipes/plus-media-format.pipe";

interface FormatMeta {
  cssVar: string;
  icon: string;
}

const FORMAT_META: Record<PlusMediaFormat, FormatMeta> = {
  [PlusMediaFormat.Manga]:      { cssVar: '--media-format-pill-manga-color',       icon: 'fa-book-open'  },
  [PlusMediaFormat.LightNovel]: { cssVar: '--media-format-pill-light-novel-color', icon: 'fa-book'       },
  [PlusMediaFormat.Comic]:      { cssVar: '--media-format-pill-comic-color',       icon: 'fa-border-all' },
  [PlusMediaFormat.Book]:       { cssVar: '--media-format-pill-book-color',        icon: 'fa-bookmark'   },
};

@Component({
  selector: 'app-media-format-pill',
  imports: [PlusMediaFormatPipe],
  templateUrl: './media-format-pill.component.html',
  styleUrl: './media-format-pill.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MediaFormatPillComponent {
  format = input.required<PlusMediaFormat>();

  protected meta = computed(() => FORMAT_META[this.format()]);
}
