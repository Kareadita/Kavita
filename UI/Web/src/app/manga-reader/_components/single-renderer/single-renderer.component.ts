import {DOCUMENT} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  OnInit,
  signal,
  Signal,
  viewChild
} from '@angular/core';
import {Observable, tap} from 'rxjs';
import {LayoutMode} from '../../_models/layout-mode';
import {FITTING_OPTION, PAGING_DIRECTION} from '../../_models/reader-enums';
import {ReaderSetting} from '../../_models/reader-setting';
import {ImageRenderer} from '../../_models/renderer';
import {MangaReaderService} from '../../_service/manga-reader.service';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {SafeStylePipe} from '../../../_pipes/safe-style.pipe';
import {ImageZoomDirective} from '../../../_directives/image-zoom.directive';
import {ReadingProfile} from "../../../_models/preferences/reading-profiles";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {ReaderMode} from "../../../_models/preferences/reader-mode";

@Component({
    selector: 'app-single-renderer',
    templateUrl: './single-renderer.component.html',
    styleUrls: ['./single-renderer.component.scss'],
    imports: [SafeStylePipe, ImageZoomDirective],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SingleRendererComponent implements OnInit, ImageRenderer {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly document = inject<Document>(DOCUMENT);
  protected readonly mangaReaderService = inject(MangaReaderService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);

  readonly readerSettings$ = input.required<Observable<ReaderSetting>>();
  readonly readingProfile = input.required<ReadingProfile>();
  readonly image$ = input.required<Observable<HTMLImageElement | null>>();
  readonly bookmark$ = input.required<Observable<number>>();
  readonly showClickOverlay$ = input.required<Observable<boolean>>();
  readonly pageNum$ = input.required<Observable<{pageNum: number, maxPages: number}>>();

  readonly imageElement = viewChild<ElementRef<HTMLImageElement>>('image');

  currentImage!: HTMLImageElement;

  private readerSettings!: Signal<ReaderSetting>;
  private showClickOverlay!: Signal<boolean>;

  protected readonly pageNum = signal(0);

  protected readonly layoutMode = computed(() => this.readerSettings().layoutMode);
  protected readonly pageSplit = computed(() => this.readerSettings().pageSplit);
  protected readonly darkness = computed(() => 'brightness(' + this.readerSettings().darkness + '%)');
  protected readonly showClickOverlayClass = computed(() => this.showClickOverlay() ? 'blur' : '');
  protected readonly readerModeClass = computed(() => {
    const mode = this.readerSettings().readerMode;
    return mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none';
  });

  protected readonly isValid = computed(() => this.layoutMode() === LayoutMode.Single);

  protected readonly emulateBookClass = computed(() => {
    const emulateBook = this.readerSettings().emulateBook;
    if (!emulateBook || !this.isValid()) return '';

    return 'book-shadow';
  });

  protected readonly widthOverride = computed(() => {
    const breakpoint = this.breakpointService.activeBreakpoint();
    const value = this.readerSettings().widthSlider;

    if (breakpoint <= this.readingProfile().disableWidthOverride) {
      return '';
    }
    return (value <= 0) ? '' : value + '%';
  });

  protected readonly imageContainerHeight = computed(() =>
    this.readerSettings().fitting === FITTING_OPTION.HEIGHT ? 'calc(100dvh)' : '');

  protected readonly imageFitClass = computed(() => {
    if (
      this.mangaReaderService.isWidePage(this.pageNum()) &&
      this.mangaReaderService.shouldRenderAsFitSplit(this.pageSplit())
      ) {
      // Rewriting to fit to width for this cover image
      return FITTING_OPTION.WIDTH + ' fit-to-screen wide';
    }

    return this.readerSettings().fitting;
  });

  ngOnInit(): void {
    this.readerSettings = toSignal(this.readerSettings$(), {injector: this.injector, requireSync: true});
    this.showClickOverlay = toSignal(this.showClickOverlay$(), {injector: this.injector, initialValue: false});

    this.pageNum$().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(pageInfo => this.pageNum.set(pageInfo.pageNum))
    ).subscribe();

    // currentImage is the same element across renders, so a load that lands later needs the view re-checked
    this.image$().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(() => this.cdRef.markForCheck())
    ).subscribe();

    this.bookmark$().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(_ => {
        const elements = [];
        const image1 = this.document.querySelector('#image-1');
        if (image1 != null) elements.push(image1);
        this.mangaReaderService.applyBookmarkEffect(elements);
      })
    ).subscribe();
  }

  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.isValid()) return;

    this.currentImage = img[0];
    this.cdRef.markForCheck();
  }

  getPageAmount(direction: PAGING_DIRECTION): number {
    if (!this.isValid() || this.mangaReaderService.shouldSplit(this.currentImage, this.pageSplit())) return 0;
    return 1;
  }
  reset(): void {}

  getBookmarkPageCount(): number {
    return 1;
  }
}
