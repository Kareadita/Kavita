import {ChangeDetectionStrategy, Component, computed, ElementRef, input, output, viewChild} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {ChapterPreviewComponent} from "../chapter-preview/chapter-preview.component";
import {PullState, PullToLoadComponent} from "../pull-to-load/pull-to-load.component";
import {ChapterCapInfo} from "../../../_models/readers/chapter-cap-info";

@Component({
  selector: 'app-chapter-cap',
  imports: [TranslocoDirective, ChapterPreviewComponent, PullToLoadComponent],
  templateUrl: './chapter-cap.component.html',
  styleUrl: './chapter-cap.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.start]': "position() === 'start'",
    '[class.end]': "position() === 'end'",
    '[class.horizontal]': 'horizontal()',
    '[class.compact]': 'compact()',
    '[class.hidden]': '!visible()',
    '[style.color]': 'textColor()',
  }
})
export class ChapterCapComponent {
  private readonly pullToLoad = viewChild(PullToLoadComponent);

  readonly position = input.required<'start' | 'end'>();
  readonly info = input.required<ChapterCapInfo>();
  /**
   * Hidden caps stay in the DOM, the PDF reader moves the host element into pdf.js's viewer
   */
  readonly visible = input<boolean>(true);
  /**
   * Laid out inline beside the pages, with no pull-to-load
   */
  readonly horizontal = input<boolean>(false);
  /**
   * No gap between the cap and the pages
   */
  readonly compact = input<boolean>(false);
  readonly scrollContainer = input<ElementRef | HTMLElement | undefined>(undefined);
  readonly backgroundColor = input<string | undefined>(undefined);
  readonly textColor = input<string | undefined>(undefined);

  readonly activated = output<void>();

  readonly pullState = computed(() => this.pullToLoad()?.state() ?? PullState.Idle);
}
