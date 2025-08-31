import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Input,
  model,
  OnInit,
  Output,
} from '@angular/core';
import {fromEvent, merge, of} from "rxjs";
import {catchError, debounceTime, tap} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {ReaderService} from "../../../_services/reader.service";
import {ToastrService} from "ngx-toastr";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {KEY_CODES} from "../../../shared/_services/utility.service";
import {EpubReaderMenuService} from "../../../_services/epub-reader-menu.service";
import {Annotation} from "../../_models/annotations/annotation";

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
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) seriesId!: number;
  @Input({required: true}) volumeId!: number;
  @Input({required: true}) chapterId!: number;
  @Input({required: true}) pageNumber: number = 0;
  @Input({required: true}) parent: ElementRef | undefined;
  @Output() refreshToC: EventEmitter<void> = new EventEmitter();
  @Output() isOpen: EventEmitter<boolean> = new EventEmitter(false);

  startXPath: string = '';
  endXPath: string = '';
  allTextFromSelection: string = '';
  selectedText: string = '';
  mode: BookLineOverlayMode = BookLineOverlayMode.None;
  bookmarkForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
  });
  hasSelectedAnnotation = model<boolean>(false);


  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly readerService = inject(ReaderService);
  private readonly toastr = inject(ToastrService);
  private readonly elementRef = inject(ElementRef);
  private readonly epubMenuService = inject(EpubReaderMenuService);


  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.ESC_KEY) {
      this.reset();
      this.cdRef.markForCheck();
      event.stopPropagation();
      event.preventDefault();
      return;
    }
  }


  ngOnInit() {
    if (this.parent) {

      const mouseUp$ = fromEvent<MouseEvent>(this.parent.nativeElement, 'mouseup');
      const touchEnd$ = fromEvent<TouchEvent>(this.parent.nativeElement, 'touchend');

      merge(mouseUp$, touchEnd$)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          debounceTime(20),  // Need extra time for this extension to inject DOM https://github.com/Kareadita/Kavita/issues/3521
          tap((event: MouseEvent | TouchEvent) => this.handleEvent(event))
        ).subscribe();
    }
  }

  handleEvent(event: MouseEvent | TouchEvent) {
    const selection = window.getSelection();
    if (!event.target) return;


    // NOTE: This doesn't account for a partial occlusion with an annotation
    this.hasSelectedAnnotation.set((event.target as HTMLElement).classList.contains('epub-highlight'));

    if ((selection === null || selection === undefined || selection.toString().trim() === ''
      || selection.toString().trim() === this.selectedText) || this.hasSelectedAnnotation()) {
      if (this.selectedText !== '') {
        event.preventDefault();
        event.stopPropagation();
      }

      const isRightClick = (event instanceof MouseEvent && event.button === 2);
      if (!isRightClick) {
        this.reset();
      }

      return;
    }

    this.selectedText = selection ? selection.toString().trim() : '';


    if (this.selectedText.length > 0 && this.mode === BookLineOverlayMode.None) {

      // Get the range from the selection
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
      this.allTextFromSelection = (event.target as Element).textContent || '';

      this.isOpen.emit(true);
      event.preventDefault();
      event.stopPropagation();
    }
    this.cdRef.markForCheck();
  }

  switchMode(mode: BookLineOverlayMode) {
    this.mode = mode;
    this.cdRef.markForCheck();
    if (this.mode === BookLineOverlayMode.Bookmark) {
      this.bookmarkForm.get('name')?.setValue(this.selectedText);
      this.focusOnBookmarkInput();
      return;
    }

    // On mobile, first selection might not match as users can select after the fact. Recalculate
    const windowText = window.getSelection();
    const selectedText = windowText?.toString() === '' ? this.selectedText : windowText?.toString() ?? this.selectedText;

    if (this.mode === BookLineOverlayMode.Annotate) {

      const createAnnotation = {
        id: 0,
        xPath: this.startXPath,
        endingXPath: this.endXPath,
        selectedText: selectedText,
        comment: '',
        containsSpoiler: false,
        pageNumber: this.pageNumber,
        selectedSlotIndex: 0,
        chapterTitle: '',
        highlightCount: selectedText.length,
        ownerUserId: 0,
        ownerUsername: '',
        createdUtc: '',
        lastModifiedUtc: '',
        context: this.allTextFromSelection,
        chapterId: this.chapterId,
        libraryId: this.libraryId,
        volumeId: this.volumeId,
        seriesId: this.seriesId,
      } as Annotation;

      this.epubMenuService.openCreateAnnotationDrawer(createAnnotation, () => {
        this.reset();
      });
    }
  }

  createPTOC() {
    const xpath = this.readerService.descopeBookReaderXpath(this.startXPath);

    this.readerService.createPersonalToC(this.libraryId, this.seriesId, this.volumeId, this.chapterId, this.pageNumber,
      this.bookmarkForm.get('name')?.value, xpath, this.selectedText).pipe(catchError(err => {
        this.focusOnBookmarkInput();
        return of();
    })).subscribe(() => {
      this.reset();
      this.refreshToC.emit();
      this.cdRef.markForCheck();
    });
  }

  focusOnBookmarkInput() {
    if (this.mode !== BookLineOverlayMode.Bookmark) return;
    setTimeout(() => this.elementRef.nativeElement.querySelector('#bookmark-name')?.focus(), 10);
  }

  reset() {
    this.bookmarkForm.reset();
    this.mode = BookLineOverlayMode.None;
    this.startXPath = '';
    this.endXPath = '';

    this.selectedText = '';
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
