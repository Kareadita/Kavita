import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  model,
  Signal,
  ViewChild,
  ViewContainerRef
} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {AnnotationService} from "../../../../_services/annotation.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {Annotation} from "../../../_models/annotations/annotation";
import {QuillEditorComponent, QuillViewComponent} from "ngx-quill";
import {TranslocoDirective} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, switchMap} from "rxjs/operators";
import {of} from "rxjs";
import {HighlightBarComponent} from "../../_annotations/highlight-bar/highlight-bar.component";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {User} from "../../../../_models/user";
import {DomSanitizer, SafeHtml} from "@angular/platform-browser";
import {DOCUMENT, NgStyle} from "@angular/common";
import {SafeHtmlPipe} from "../../../../_pipes/safe-html.pipe";
import {EpubHighlightService} from "../../../../_services/epub-highlight.service";

export enum AnnotationMode {
  View = 0,
  Edit = 1,
  Create = 2,
}

const INIT_HIGHLIGHT_DELAY = 200;

@Component({
  selector: 'app-view-edit-annotation-drawer',
  imports: [
    QuillEditorComponent,
    ReactiveFormsModule,
    QuillViewComponent,
    TranslocoDirective,
    HighlightBarComponent,
    NgStyle
  ],
  templateUrl: './view-edit-annotation-drawer.component.html',
  styleUrl: './view-edit-annotation-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ViewEditAnnotationDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly annotationService = inject(AnnotationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly highlightSlotPipe = new SlotColorPipe();
  private readonly document = inject(DOCUMENT);
  private readonly safeHtml = new SafeHtmlPipe();
  private readonly sanitizer = inject(DomSanitizer);
  private readonly epubHighlightService = inject(EpubHighlightService);

  @ViewChild('renderTarget', {read: ViewContainerRef}) renderTarget!: ViewContainerRef;

  annotation = model<Annotation | null>(null);
  mode = model<AnnotationMode>(AnnotationMode.View);
  user = model<User | null>(null);
  isEditMode: Signal<boolean>
  isEditOrCreateMode: Signal<boolean>
  titleColor: Signal<string>;
  totalText!: Signal<SafeHtml>;


  formGroup!: FormGroup;
  annotationNote: object = {};
  isSetup = false;

  constructor() {
    this.titleColor = computed(() => {
      const annotation = this.annotation();
      if (!annotation) return '';
      // TODO: Safefty check
      return this.highlightSlotPipe.transform(this.annotationService.slots()[annotation.selectedSlotIndex].color);
    });

    this.isEditMode = computed(() => {
      const mode = this.mode();
      return mode === AnnotationMode.Edit;
    });
    this.isEditOrCreateMode = computed(() => {
      const mode = this.mode();
      return mode === AnnotationMode.Edit || mode === AnnotationMode.Create;
    });

    this.totalText = computed(() => {
      const highlightAnnotation = this.annotation();
      const isCreateFlow =  this.mode() === AnnotationMode.Create;
      if (highlightAnnotation == null || highlightAnnotation?.context === null) return '';

      const contextText = highlightAnnotation.context;
      const selectedText = highlightAnnotation.selectedText!;

      const annotationId = isCreateFlow ? 0 : highlightAnnotation.id;

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
        }, INIT_HIGHLIGHT_DELAY);

        return this.sanitizer.bypassSecurityTrustHtml(`<app-epub-highlight id="epub-highlight-${annotationId}">${this.safeHtml.transform(selectedText)}</app-epub-highlight>${this.safeHtml.transform(trimmedAfterText)}`);
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
      }, INIT_HIGHLIGHT_DELAY);
      return this.sanitizer.bypassSecurityTrustHtml(`${this.safeHtml.transform(beforeText)}<app-epub-highlight id="epub-highlight-${annotationId}">${this.safeHtml.transform(selectedText)}</app-epub-highlight>${this.safeHtml.transform(trimmedAfterText)}`);
    });


    effect(() => {
      const isEditMode = this.isEditMode();
      const annotation = this.annotation();

      // Side effect - patch in the current note
      // Parse the stored JSON string back to Delta object
      this.annotationNote = annotation?.comment ? JSON.parse(annotation.comment) : {}
      this.formGroup =  new FormGroup({
        'note': new FormControl(this.annotationNote, []),
        'hasSpoiler': new FormControl(annotation?.containsSpoiler, []),
        'selectedSlotIndex': new FormControl(annotation?.selectedSlotIndex ?? 0, [])
      });

      if (isEditMode && !this.isSetup) {
        this.formGroup.valueChanges.pipe(
          debounceTime(350),
          switchMap(_ => {
            if (!annotation) return of();

            annotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
            annotation.comment = JSON.stringify(this.annotationNote);

            return this.annotationService.updateAnnotation(annotation);
          }),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe();

        this.isSetup = true;
      }
    });
  }

  createAnnotation() {
    const highlightAnnotation = this.annotation();
    if (!highlightAnnotation) return;

    highlightAnnotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
    highlightAnnotation.comment = JSON.stringify(this.annotationNote);
    // For create annotation, we have to have this hack
    highlightAnnotation.createdUtc = '0001-01-01T00:00:00Z';
    highlightAnnotation.lastModifiedUtc = '0001-01-01T00:00:00Z'

    console.log('saving highlight: ', highlightAnnotation);
    this.annotationService.createAnnotation(highlightAnnotation).subscribe(_ => {
      this.close();
    });
  }

  updateSlotColor() {
    // TODO: This will emit slotUpdate and update the user preferences

  }



  changeSlotIndex(slotIndex: number) {
    const annotation = this.annotation();

    if (annotation) {
      this.annotation.set({...annotation, selectedSlotIndex: slotIndex});
      this.formGroup.get('selectedSlotIndex')?.setValue(slotIndex);
    }
  }


  close() {
    this.activeOffcanvas.close();
  }

  updateContent(event: any) {
    this.annotationNote = event.content;
  }

  private initHighlights() {
    const highlightAnnotation = this.annotation();
    if (highlightAnnotation === null) return;

    // Clear any existing components first
    this.renderTarget.clear();

    const parentElem = this.document.querySelector('#render-target');
    this.epubHighlightService.initializeHighlightElements([highlightAnnotation], this.renderTarget, parentElem, {showIcon: false, showHighlight: true});
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

  protected readonly AnnotationMode = AnnotationMode;
}
