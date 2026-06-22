import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import {fromEvent, merge, of} from "rxjs";
import {catchError, debounceTime, tap} from "rxjs/operators";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {ReaderService} from "../../../_services/reader.service";
import {ToastrService} from "ngx-toastr";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {EpubReaderMenuService} from "../../../_services/epub-reader-menu.service";
import {Annotation} from "../../_models/annotations/annotation";
import {isMobileChromium} from "../../../_helpers/browser";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {KeyBindService} from "../../../_services/key-bind.service";
import {KeyBindTarget} from "../../../_models/preferences/preferences";

enum BookLineOverlayMode {
  None = 0,
  Annotate = 1,
  Bookmark = 2
}

@Component({
    selector: 'app-book-line-overlay',
    imports: [ReactiveFormsModule, TranslocoDirective],
    templateUrl: './book-line-overlay.component.html',
    styleUrls: ['./book-line-overlay.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class BookLineOverlayComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly readerService = inject(ReaderService);
  private readonly toastr = inject(ToastrService);
  private readonly elementRef = inject(ElementRef);
  private readonly epubMenuService = inject(EpubReaderMenuService);
  private readonly keyBindService = inject(KeyBindService);

  libraryId = input.required<number>();
  seriesId = input.required<number>();
  volumeId = input.required<number>();
  chapterId = input.required<number>();
  pageNumber = input.required<number>();
  parent = input.required<ElementRef | undefined>();

  refreshToC = output();
  isOpen = output<boolean>();


  startXPath: string = '';
  endXPath: string = '';
  allTextFromSelection: string = '';
  selectedText = signal<string>('');
  mode = signal<BookLineOverlayMode>(BookLineOverlayMode.None);
  bookmarkForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
  });
  hasSelectedAnnotation = signal<boolean>(false);
  showOverlay = computed(() => this.selectedText().length > 0 || this.mode() !== BookLineOverlayMode.None);



  ngOnInit() {
    // Check for Pointer Events API support
    const hasPointerEvents = 'PointerEvent' in window;

    // Some mobile Chromium browsers do not send touchend events reliably: https://github.com/Kareadita/Kavita/issues/4072
    if (hasPointerEvents && !isMobileChromium()) {
      // Use pointer events for modern browsers (except problematic mobile Chromium)
      this.setupPointerEventListener();
    } else {
      // Fallback to mouse/touch events
      this.setupLegacyEventListeners();
    }

    this.keyBindService.registerListener(
      this.destroyRef,
      () => this.reset(),
      [KeyBindTarget.Escape],
    );
  }

  private setupPointerEventListener(): void {
    const parent = this.parent();
    if (!parent) return;

    fromEvent<PointerEvent>(parent.nativeElement, 'pointerup')
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        debounceTime(20),
        tap((event: PointerEvent) => this.handlePointerEvent(event))
      ).subscribe();
  }

  private setupLegacyEventListeners(): void {
    const parent = this.parent();
    if (!parent) return;

    const mouseUp$ = fromEvent<MouseEvent>(parent.nativeElement, 'mouseup');
    const touchEnd$ = fromEvent<TouchEvent>(parent.nativeElement, 'touchend');

    // Additional events for mobile Chromium workaround
    const additionalEvents$ = isMobileChromium() ? [
      fromEvent<TouchEvent>(parent.nativeElement, 'touchcancel'),
      fromEvent<PointerEvent>(parent.nativeElement, 'pointerup')
    ] : [];

    merge(mouseUp$, touchEnd$, ...additionalEvents$)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        debounceTime(20),
        tap((event: MouseEvent | TouchEvent | PointerEvent) => this.handleLegacyEvent(event))
      ).subscribe();
  }

  private handlePointerEvent(event: PointerEvent): void {
    // Filter out pen/stylus events if you don't want to handle them
    if (event.pointerType === 'pen') {
      return;
    }

    // Check for right-click
    const isRightClick = event.button === 2;

    this.processSelectionEvent(event, isRightClick);
  }

  private handleLegacyEvent(event: MouseEvent | TouchEvent | PointerEvent): void {
    // Determine if it's a right-click (only applicable to mouse events)
    const isRightClick = event instanceof MouseEvent && event.button === 2;

    this.processSelectionEvent(event, isRightClick);
  }

  private processSelectionEvent(event: Event, isRightClick: boolean): void {
    if (!event.target) return;

    const selection = window.getSelection();

    // Check if target has annotation class
    this.hasSelectedAnnotation.set((event.target as HTMLElement).classList.contains('epub-highlight'));

    if (this.shouldSkipSelection(selection, isRightClick)) {
      if (this.selectedText() !== '') {
        event.preventDefault();
        event.stopPropagation();
      }

      if (!isRightClick) {
        this.reset();
      }
      return;
    }

    // Process valid selection
    this.selectedText.set(selection ? selection.toString().trim() : '');

    if (this.selectedText().length > 0 && this.mode() === BookLineOverlayMode.None && selection !== null) {
      this.captureSelectionContext(selection, event.target as Element);

      this.isOpen.emit(true);
      event.preventDefault();
      event.stopPropagation();
    }

    this.cdRef.markForCheck();
  }

  private shouldSkipSelection(selection: Selection | null, isRightClick: boolean): boolean {
    return (selection === null ||
        selection === undefined ||
        selection.toString().trim() === '' ||
        selection.toString().trim() === this.selectedText()) ||
      this.hasSelectedAnnotation();
  }

  /**
   * Captures XPath and context for the current selection
   */
  private captureSelectionContext(selection: Selection, targetElement: Element): void {
    const range = selection.getRangeAt(0);

    // Get start and end containers
    const startContainer = this.getElementContainer(range.startContainer);
    const endContainer = this.getElementContainer(range.endContainer);

    // Generate XPaths for both start and end
    this.startXPath = this.readerService.getXPathTo(startContainer);
    this.endXPath = this.readerService.getXPathTo(endContainer);

    // Protect from DOM Shift by removing the UI part and making this scoped to true epub html
    this.startXPath = this.readerService.descopeBookReaderXpath(this.startXPath);
    this.endXPath = this.readerService.descopeBookReaderXpath(this.endXPath);

    // Get the context window for generating a blurb in annotation flow
    this.allTextFromSelection = targetElement.textContent || '';
  }


  switchMode(mode: BookLineOverlayMode) {
    this.mode.set(mode);

    // On mobile, first selection might not match as users can select after the fact. Recalculate
    const windowText = window.getSelection();
    const selectedText = windowText?.toString() === '' ? this.selectedText() : windowText?.toString() ?? this.selectedText();

    if (mode === BookLineOverlayMode.Bookmark) {
      this.bookmarkForm.get('name')?.setValue(selectedText);
      this.focusOnBookmarkInput();
      return;
    }



    if (mode === BookLineOverlayMode.Annotate) {
      const createAnnotation = {
        id: 0,
        xPath: this.startXPath,
        endingXPath: this.endXPath,
        selectedText: selectedText,
        comment: '',
        containsSpoiler: false,
        pageNumber: this.pageNumber(),
        selectedSlotIndex: 0,
        chapterTitle: '',
        highlightCount: selectedText.length,
        ownerUserId: 0,
        ownerUsername: '',
        createdUtc: '',
        lastModifiedUtc: '',
        context: this.allTextFromSelection,
        chapterId: this.chapterId(),
        libraryId: this.libraryId(),
        volumeId: this.volumeId(),
        seriesId: this.seriesId(),
      } as Annotation;

      this.epubMenuService.openCreateAnnotationDrawer(createAnnotation, () => {
        this.reset();
      });
    }
  }

  createPTOC() {
    const xpath = this.readerService.descopeBookReaderXpath(this.startXPath);

    this.readerService.createPersonalToC(this.libraryId(), this.seriesId(), this.volumeId(), this.chapterId(), this.pageNumber(),
      this.bookmarkForm.get('name')?.value, xpath, this.selectedText()).pipe(catchError(err => {
        this.focusOnBookmarkInput();
        return of();
    })).subscribe(() => {
      this.reset();
      this.refreshToC.emit(undefined);
      this.cdRef.markForCheck();
    });
  }

  focusOnBookmarkInput() {
    if (this.mode() !== BookLineOverlayMode.Bookmark) return;
    setTimeout(() => this.elementRef.nativeElement.querySelector('#bookmark-name')?.focus(), 10);
  }

  reset() {
    this.bookmarkForm.reset();
    this.mode.set(BookLineOverlayMode.None);
    this.startXPath = '';
    this.endXPath = '';

    this.selectedText.set('');
    this.allTextFromSelection = '';
    const selection = window.getSelection();
    if (selection) {
      selection.removeAllRanges();
    }
    this.isOpen.emit(false);
    this.cdRef.markForCheck();
  }

  async copy() {
    const selection = window.getSelection();
    if (selection) {
      await navigator.clipboard.writeText(selection.toString());
      this.toastr.info(translate('toasts.copied-to-clipboard'));
    }
    this.reset();
  }

  private getElementContainer(node: Node): Element {
    // If the node is a text node, get its parent element
    // If it's already an element, return it
    return node.nodeType === Node.TEXT_NODE ? node.parentElement! : node as Element;
  }

  protected readonly BookLineOverlayMode = BookLineOverlayMode;

}
