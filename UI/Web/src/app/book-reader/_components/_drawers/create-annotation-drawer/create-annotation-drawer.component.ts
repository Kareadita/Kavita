import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  model,
  Signal,
  ViewChild,
  ViewContainerRef
} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {CreateAnnotationRequest} from "../../../_models/create-annotation-request";
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../../_pipes/safe-html.pipe";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {DOCUMENT} from "@angular/common";
import {allHighlightColors, Annotation, HighlightColor} from "../../../_models/annotations/annotation";
import {EpubHighlightService} from "../../../../_services/epub-highlight.service";
import {DomSanitizer, SafeHtml} from "@angular/platform-browser";
import {QuillModule} from "ngx-quill";
import {AnnotationService} from "../../../../_services/annotation.service";

@Component({
    selector: 'app-create-annotation-drawer',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    QuillModule
  ],
    templateUrl: './create-annotation-drawer.component.html',
    styleUrl: './create-annotation-drawer.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
  })
  export class CreateAnnotationDrawerComponent {
    private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
    private readonly annotationService = inject(AnnotationService);
    private readonly epubHighlightService = inject(EpubHighlightService);
    private readonly document = inject(DOCUMENT);
    private readonly safeHtml = new SafeHtmlPipe();
    private readonly sanitizer = inject(DomSanitizer);

    createAnnotation = model<CreateAnnotationRequest | null>(null);
    totalText!: Signal<SafeHtml>;

    formGroup!: FormGroup;
    annotationNote = {};

    @ViewChild('renderTarget', { read: ViewContainerRef }) renderTarget!: ViewContainerRef;


    constructor() {

      this.formGroup =  new FormGroup({
        'note': new FormControl(this.createAnnotation()?.comment || '', []),
        'hasSpoiler': new FormControl(false, [])
      });

      this.totalText = computed(() => {
        // TODO: See if we can move this to annotation service and just use the view-edit-annotation-drawer to streamline all logic
        const annotation = this.createAnnotation();
        console.log('Calculating totalText()', annotation);
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

          setTimeout(() => {
            this.initHighlights();
          }, 100);

          return this.sanitizer.bypassSecurityTrustHtml(`<app-epub-highlight id="epub-highlight-0">${this.safeHtml.transform(selectedText)}</app-epub-highlight>${this.safeHtml.transform(trimmedAfterText)}`);
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

        setTimeout(() => {
          this.initHighlights();
        }, 100);
        return this.sanitizer.bypassSecurityTrustHtml(`${this.safeHtml.transform(beforeText)}<app-epub-highlight id="epub-highlight-0">${this.safeHtml.transform(selectedText)}</app-epub-highlight>${this.safeHtml.transform(trimmedAfterText)}`);
      });
    }


    save() {
      const annotation = this.createAnnotation();
      if (!annotation) return;

      annotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
      annotation.comment = JSON.stringify(this.annotationNote);

      this.annotationService.createAnnotation(annotation).subscribe(res => {
        this.close();
      });
    }



    private initHighlights() {
      const annotation = this.createAnnotation();
      if (annotation === null) return;

      // Clear any existing components first
      this.renderTarget.clear();

      const properAnnotation = {
        id: 0,
        xpath: annotation.xpath,
        endingXPath: annotation.xpath,
        selectedText: annotation.selectedText,
        comment: annotation.comment,
        containsSpoiler: annotation.containsSpoiler,
        pageNumber: annotation.pageNumber,
        chapterId: annotation.chapterId,
        selectedSlotIndex: annotation.selectedSlotIndex
      } as Annotation;

      const parentElem = this.document.querySelector('#render-target');
      this.epubHighlightService.initializeHighlightElements([properAnnotation], this.renderTarget, parentElem, {showIcon: false, showHighlight: true});
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

    //
    // changeHighlight(highlight: HighlightColor) {
    //   let annotation = this.createAnnotation();
    //   if (annotation) {
    //     this.createAnnotation.set({...annotation, highlightColor: highlight});
    //   }
    // }


    close() {
      this.activeOffcanvas.close();
    }

    updateContent(event: any) {
      this.annotationNote = event.content;
    }

    cancelEvent(event: any) {
      event.stopPropagation();
      event.preventDefault();
    }

    protected readonly HighlightColor = HighlightColor;
    protected readonly allHighlightColors = allHighlightColors;
  }
