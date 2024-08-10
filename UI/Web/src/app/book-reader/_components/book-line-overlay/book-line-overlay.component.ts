import {
  ChangeDetectionStrategy, ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef, EventEmitter, HostListener,
  inject,
  Input,
  OnInit, Output,
} from '@angular/core';
import {CommonModule} from '@angular/common';
import {fromEvent, merge, of} from "rxjs";
import {catchError} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {ReaderService} from "../../../_services/reader.service";
import {ToastrService} from "ngx-toastr";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {KEY_CODES} from "../../../shared/_services/utility.service";

enum BookLineOverlayMode {
  None = 0,
  Bookmark = 1
}

@Component({
  selector: 'app-book-line-overlay',
  standalone: true,
    imports: [CommonModule, ReactiveFormsModule, TranslocoDirective],
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

  xPath: string = '';
  selectedText: string = '';
  mode: BookLineOverlayMode = BookLineOverlayMode.None;
  bookmarkForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
  });

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly readerService = inject(ReaderService);

  get BookLineOverlayMode() { return BookLineOverlayMode; }
  constructor(private elementRef: ElementRef, private toastr: ToastrService) {}

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
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((event: MouseEvent | TouchEvent) => {
          this.handleEvent(event);
        });
    }
  }

  handleEvent(event: MouseEvent | TouchEvent) {
    const selection = window.getSelection();
    if (!event.target) return;



    if ((selection === null || selection === undefined || selection.toString().trim() === '' || selection.toString().trim() === this.selectedText)) {
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
      this.xPath = this.readerService.getXPathTo(event.target);
      if (this.xPath !== '') {
        this.xPath = '//' + this.xPath;
      }

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
    }
  }

  createPTOC() {
    this.readerService.createPersonalToC(this.libraryId, this.seriesId, this.volumeId, this.chapterId, this.pageNumber,
      this.bookmarkForm.get('name')?.value, this.xPath).pipe(catchError(err => {
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
    this.xPath = '';
    this.selectedText = '';
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


}
