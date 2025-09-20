import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  model,
  OnInit,
  Signal,
  ViewChild,
  ViewContainerRef
} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {AnnotationService} from "../../../../_services/annotation.service";
import {FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule} from "@angular/forms";
import {Annotation} from "../../../_models/annotations/annotation";
import {TranslocoDirective} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, switchMap} from "rxjs/operators";
import {of} from "rxjs";
import {HighlightBarComponent} from "../../_annotations/highlight-bar/highlight-bar.component";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {User} from "../../../../_models/user";
import {DomSanitizer, SafeHtml} from "@angular/platform-browser";
import {DatePipe, DOCUMENT, NgStyle} from "@angular/common";
import {SafeHtmlPipe} from "../../../../_pipes/safe-html.pipe";
import {EpubHighlightService} from "../../../../_services/epub-highlight.service";
import {PageChapterLabelPipe} from "../../../../_pipes/page-chapter-label.pipe";
import {UserBreakpoint, UtilityService} from "../../../../shared/_services/utility.service";
import {QuillTheme, QuillWrapperComponent} from "../../quill-wrapper/quill-wrapper.component";
import {ContentChange, QuillViewComponent} from "ngx-quill";
import {UtcToLocaleDatePipe} from "../../../../_pipes/utc-to-locale-date.pipe";
import {AccountService} from "../../../../_services/account.service";
import {OffCanvasResizeComponent, ResizeMode} from "../../../../shared/_components/off-canvas-resize/off-canvas-resize.component";

export enum AnnotationMode {
  View = 0,
  Edit = 1,
  Create = 2,
}

const INIT_HIGHLIGHT_DELAY = 200;

@Component({
  selector: 'app-view-edit-annotation-drawer',
  imports: [
    QuillWrapperComponent,
    ReactiveFormsModule,
    TranslocoDirective,
    HighlightBarComponent,
    NgStyle,
    PageChapterLabelPipe,
    QuillWrapperComponent,
    QuillViewComponent,
    DatePipe,
    UtcToLocaleDatePipe,
    OffCanvasResizeComponent
  ],
  templateUrl: './view-edit-annotation-drawer.component.html',
  styleUrl: './view-edit-annotation-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ViewEditAnnotationDrawerComponent implements OnInit {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly annotationService = inject(AnnotationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly highlightSlotPipe = new SlotColorPipe();
  private readonly document = inject(DOCUMENT);
  private readonly safeHtml = new SafeHtmlPipe();
  private readonly sanitizer = inject(DomSanitizer);
  private readonly epubHighlightService = inject(EpubHighlightService);
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly utilityService = inject(UtilityService);
  protected readonly accountService = inject(AccountService);

  @ViewChild('renderTarget', {read: ViewContainerRef}) renderTarget!: ViewContainerRef;

  annotation = model<Annotation | null>(null);
  mode = model<AnnotationMode>(AnnotationMode.View);
  user = model<User | null>(null);
  isEditMode: Signal<boolean>
  isEditOrCreateMode: Signal<boolean>
  titleColor: Signal<string>;
  totalText!: Signal<SafeHtml>;


  formGroup!: FormGroup<{
    note: FormControl<object>,
    hasSpoiler: FormControl<boolean>,
    selectedSlotIndex: FormControl<number>,
  }>;
  annotationNote: object = {};

  constructor() {
    this.titleColor = computed(() => {
      const annotation = this.annotation();
      const slots = this.annotationService.slots();

      if (!annotation || annotation.selectedSlotIndex >= slots.length) return '';

      return this.highlightSlotPipe.transform(slots[annotation.selectedSlotIndex].color);
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
        setTimeout(() => {
          this.initHighlights();
        }, INIT_HIGHLIGHT_DELAY);

        return this.sanitizer.bypassSecurityTrustHtml(`<app-epub-highlight id="epub-highlight-${annotationId}">${this.safeHtml.transform(selectedText)}</app-epub-highlight>`);
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

    this.formGroup = this.fb.group({
      note: this.fb.control<object>({}, []),
      hasSpoiler: this.fb.control<boolean>(false, []),
      selectedSlotIndex: this.fb.control<number>(0, []),
    });

    effect(() => {
      const editMode = this.isEditMode();
      if (!editMode) return;

      this.formGroup.valueChanges.pipe(
        debounceTime(350),
        switchMap(_ => {
          const updatedAnnotation = this.annotation();
          if (!updatedAnnotation) return of();

          updatedAnnotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
          updatedAnnotation.comment = JSON.stringify(this.annotationNote);

          return this.annotationService.updateAnnotation(updatedAnnotation);
        }),
        takeUntilDestroyed(this.destroyRef)
      ).subscribe();
    });
  }

  ngOnInit(){
    const annotation = this.annotation();
    if (annotation) {
      this.annotationNote = annotation?.comment ? JSON.parse(annotation.comment) : {};
      this.formGroup.get('note')!.setValue(this.annotationNote);
      this.formGroup.get('hasSpoiler')!.setValue(annotation.containsSpoiler);
      this.formGroup.get('selectedSlotIndex')!.setValue(annotation.selectedSlotIndex);
    }
  }

  createAnnotation() {
    const highlightAnnotation = this.annotation();
    if (!highlightAnnotation) return;

    highlightAnnotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
    highlightAnnotation.comment = JSON.stringify(this.annotationNote);
    // For create annotation, we have to have this hack
    highlightAnnotation.createdUtc = '0001-01-01T00:00:00Z';
    highlightAnnotation.lastModifiedUtc = '0001-01-01T00:00:00Z'

    this.annotationService.createAnnotation(highlightAnnotation).subscribe(_ => {
      this.close();
    });
  }

  switchToEditMode() {
    if (this.isEditMode()) return;

    const annotation = this.annotation();
    if (annotation == null || annotation.ownerUsername !== this.accountService.currentUserSignal()?.username) return;

    this.mode.set(AnnotationMode.Edit);
  }

  changeSlotIndex(slotIndex: number) {
    const annotation = this.annotation();

    if (annotation) {
      console.log('view-edit drawer, slot index changed: ', slotIndex, 'comment: ', this.annotation()?.comment, 'form comment: ', this.formGroup.get('note')?.value);
      this.annotation.set({...annotation, selectedSlotIndex: slotIndex});
      this.formGroup.get('selectedSlotIndex')?.setValue(slotIndex);

      // Patch back in any text in the quill editor
      console.log('(2) view-edit drawer, slot index changed: ', slotIndex, 'comment: ', this.annotation()?.comment, 'form comment: ', this.formGroup.get('note')?.value);
    }
  }

  close() {
    this.activeOffcanvas.close();
  }

  updateContent(event: ContentChange) {
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
  protected readonly UserBreakpoint = UserBreakpoint;
  protected readonly QuillTheme = QuillTheme;
  protected readonly ResizeMode = ResizeMode;
  protected readonly window = window;
}
