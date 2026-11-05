import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {ImageService} from "../../../_services/image.service";
import {ImageComponent} from "../../image/image.component";

@Component({
  selector: 'app-chapter-preview',
  imports: [ImageComponent],
  templateUrl: './chapter-preview.component.html',
  styleUrl: './chapter-preview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterPreviewComponent {
  private readonly imageService = inject(ImageService);

  /**
   * When set, the chapter's cover fills the card behind the text
   */
  readonly chapterId = input<number | undefined>(undefined);
  readonly heading = input.required<string>();
  readonly subheading = input<string>('');
  readonly hint = input<string>('');

  readonly activated = output<void>();

  protected readonly coverUrl = computed(() => {
    const chapterId = this.chapterId();
    return chapterId === undefined ? '' : this.imageService.getChapterCoverImage(chapterId);
  });
}
