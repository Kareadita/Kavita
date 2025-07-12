import {ChangeDetectionStrategy, Component, computed, inject, model, Signal} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {CreateAnnotationRequest} from "../../../_models/create-annotation-request";
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-create-annotation-drawer',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './create-annotation-drawer.component.html',
  styleUrl: './create-annotation-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateAnnotationDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly safeHtml = new SafeHtmlPipe();

  createAnnotation = model<CreateAnnotationRequest | null>(null);
  totalText!: Signal<string>;

  constructor() {
    this.totalText = computed(() => {
      const annotation = this.createAnnotation();
      if (annotation == null || annotation?.context === null) return '';

      const contextText = annotation.context;
      const selectedText = annotation.selectedText!;

      if (!contextText.includes(selectedText)) {
        return selectedText;
      }

      // Get estimated character capacity for 2 lines
      const estimatedCapacity = this.estimateCharacterCapacity('render-target') * 2;

      // If selected text alone is too long, just show it
      if (selectedText.length >= estimatedCapacity) {
        return `<span class="fw-bold">${this.safeHtml.transform(selectedText)}</span>`;
      }

      // Find the position of selected text in context
      const selectedIndex = contextText.indexOf(selectedText);
      const selectedEndIndex = selectedIndex + selectedText.length;

      // Check if selected text follows punctuation (smart context detection)
      const shouldIgnoreBeforeContext = this.isSelectedTextAfterPunctuation(contextText, selectedIndex);

      // Extract after text first to see if we have content after
      const afterText = contextText.substring(selectedEndIndex);

      // If selected text follows punctuation AND we have after content, ignore before context
      if (shouldIgnoreBeforeContext && afterText.trim().length > 0) {
        const availableCapacity = estimatedCapacity - selectedText.length;
        const trimmedAfterText = this.extractAfterContext(afterText, availableCapacity);

        return `<span class="fw-bold">${this.safeHtml.transform(selectedText)}</span>${this.safeHtml.transform(trimmedAfterText)}`;
      }

      // Otherwise, use normal context distribution
      const remainingCapacity = estimatedCapacity - selectedText.length;
      const beforeCapacity = Math.floor(remainingCapacity * 0.4); // 40% before
      const afterCapacity = remainingCapacity - beforeCapacity;   // 60% after

      // Extract context portions
      let beforeText = contextText.substring(0, selectedIndex);
      let trimmedAfterText = afterText;

      // Trim context to fit capacity
      if (beforeText.length > beforeCapacity) {
        beforeText = '...' + beforeText.substring(beforeText.length - beforeCapacity + 3);
        // Try to break at word boundary
        const spaceIndex = beforeText.indexOf(' ', 3);
        if (spaceIndex !== -1 && spaceIndex < beforeCapacity * 0.8) {
          beforeText = '...' + beforeText.substring(spaceIndex + 1);
        }
      }

      if (trimmedAfterText.length > afterCapacity) {
        trimmedAfterText = trimmedAfterText.substring(0, afterCapacity - 3) + '...';
        // Try to break at word boundary
        const lastSpaceIndex = trimmedAfterText.lastIndexOf(' ', afterCapacity - 3);
        if (lastSpaceIndex !== -1 && lastSpaceIndex > afterCapacity * 0.8) {
          trimmedAfterText = trimmedAfterText.substring(0, lastSpaceIndex) + '...';
        }
      }

      return `${this.safeHtml.transform(beforeText)}<span class="fw-bold">${this.safeHtml.transform(selectedText)}</span>${this.safeHtml.transform(afterText)}`;
    });

  }

  private isSelectedTextAfterPunctuation(contextText: string, selectedIndex: number): boolean {
    if (selectedIndex === 0) return false;

    // Look backwards from the selected text to find the last non-whitespace character
    let checkIndex = selectedIndex - 1;

    // Skip whitespace
    while (checkIndex >= 0 && /\s/.test(contextText[checkIndex])) {
      checkIndex--;
    }

    // If we found a character, check if it's punctuation
    if (checkIndex >= 0) {
      const lastChar = contextText[checkIndex];
      // Define sentence-ending punctuation
      const sentenceEnders = ['.', '!', '?', '"', "'", ')', ']', '—', '–'];
      return sentenceEnders.includes(lastChar);
    }

    return false;
  }

  private extractAfterContext(afterText: string, capacity: number): string {
    if (afterText.length <= capacity) {
      return afterText;
    }

    let result = afterText.substring(0, capacity - 3) + '...';

    // Try to break at word boundary
    const lastSpaceIndex = result.lastIndexOf(' ', capacity - 3);
    if (lastSpaceIndex !== -1 && lastSpaceIndex > capacity * 0.8) {
      result = result.substring(0, lastSpaceIndex) + '...';
    }

    return result;
  }

  private estimateCharacterCapacity(elementId: string): number {
    const element = document.getElementById(elementId);
    if (!element) return 100; // fallback

    const computedStyle = window.getComputedStyle(element);
    const fontSize = parseFloat(computedStyle.fontSize);
    const avgCharWidth = fontSize * 0.6;

    const paddingLeft = parseFloat(computedStyle.paddingLeft);
    const paddingRight = parseFloat(computedStyle.paddingRight);
    const availableWidth = element.clientWidth - paddingLeft - paddingRight;

    return Math.floor(availableWidth / avgCharWidth);
  }


  close() {
    this.activeOffcanvas.close();
  }
}
