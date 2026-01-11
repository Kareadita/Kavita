import {ChangeDetectionStrategy, Component, computed, inject, input, signal} from '@angular/core';
import {NgClass} from "@angular/common";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {BreakpointService} from "../../_services/breakpoint.service";

@Component({
    selector: 'app-read-more',
    imports: [SafeHtmlPipe, TranslocoDirective, NgClass],
    templateUrl: './read-more.component.html',
    styleUrls: ['./read-more.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadMoreComponent {
  private readonly breakpointService = inject(BreakpointService);

  /**
   * String to apply read more on
   */
  readonly text = input.required<string>();
  /**
   * Max length before apply read more. Defaults to 250 characters.
   */
  readonly maxLength = input<number>(250);
  /**
   * If the field is collapsed and blur true, text will not be readable
   */
  readonly blur = input(false);
  /**
   * If the read more toggle is visible
   */
  readonly showToggle = input(true);
  /**
   * When true, maxLength is ignored and uses 170 (desktop or below) / 200 (above desktop).
   */
  readonly useResponsiveLength = input(false);

  readonly isCollapsed = signal(true);

  private readonly effectiveMaxLength = computed(() => {
    if (this.useResponsiveLength()) {
      return this.breakpointService.isDesktopOrBelow() ? 170 : 200;
    }
    return this.maxLength();
  });

  private readonly normalizedText = computed(() =>
    this.text()?.replace(/\n/g, '<br>') ?? ''
  );

  private readonly exceedsMaxLength = computed(() =>
    this.text()?.length > this.effectiveMaxLength()
  );

  readonly hideToggle = computed(() => !this.exceedsMaxLength());

  readonly currentText = computed(() => {
    const text = this.normalizedText();
    const maxLen = this.effectiveMaxLength();

    if (!text || text.length <= maxLen) {
      return text;
    }

    if (!this.isCollapsed()) {
      return text;
    }

    return this.truncateText(text, maxLen);
  });

  toggleView(): void {
    this.isCollapsed.update(v => !v);
  }

  private truncateText(text: string, maxLen: number): string {
    let truncated = text.substring(0, maxLen);

    // Find last natural breaking point: space for English, or a CJK character boundary
    const cjkMatch = truncated.match(/[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}]+$/u);
    const lastSpace = truncated.lastIndexOf(' ');

    if (lastSpace > 0) {
      truncated = truncated.substring(0, lastSpace);
    } else if (cjkMatch) {
      truncated = truncated.substring(0, truncated.length - cjkMatch[0].length);
    }

    return truncated + '…';
  }
}
