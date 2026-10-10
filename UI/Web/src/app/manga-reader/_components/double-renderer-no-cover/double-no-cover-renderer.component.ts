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
import {DEBUG_MODES, ImageRenderer} from '../../_models/renderer';
import {MangaReaderService} from '../../_service/manga-reader.service';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {SafeStylePipe} from '../../../_pipes/safe-style.pipe';
import {ReaderMode} from "../../../_models/preferences/reader-mode";
import {ImageZoomDirective} from '../../../_directives/image-zoom.directive';

/**
 * Renders 2 pages except on last page, and before a wide image
 */
@Component({
    selector: 'app-double-no-cover-renderer',
    templateUrl: './double-no-cover-renderer.component.html',
    styleUrls: ['./double-no-cover-renderer.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [SafeStylePipe, ImageZoomDirective]
})
export class DoubleNoCoverRendererComponent implements OnInit, ImageRenderer {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly mangaReaderService = inject(MangaReaderService);
  private readonly document = inject<Document>(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);

  readonly imageElement = viewChild<ElementRef<HTMLImageElement>>('image');

  readonly readerSettings$ = input.required<Observable<ReaderSetting>>();
  readonly bookmark$ = input.required<Observable<number>>();
  readonly showClickOverlay$ = input.required<Observable<boolean>>();
  readonly pageNum$ = input.required<Observable<{pageNum: number, maxPages: number}>>();
  readonly getPage = input.required<(pageNum: number) => HTMLImageElement>();

  debugMode: DEBUG_MODES = DEBUG_MODES.None;

  private readerSettings!: Signal<ReaderSetting>;
  private showClickOverlay!: Signal<boolean>;

  private readonly pageNum = signal(0);
  private readonly maxPages = signal(0);

  protected readonly layoutMode = computed(() => this.readerSettings().layoutMode);
  protected readonly imageFitClass = computed(() => this.readerSettings().fitting);
  protected readonly darkness = computed(() => 'brightness(' + this.readerSettings().darkness + '%)');
  protected readonly emulateBookClass = computed(() => this.readerSettings().emulateBook ? 'book-shadow' : '');
  protected readonly showClickOverlayClass = computed(() => this.showClickOverlay() ? 'blur' : '');
  protected readonly readerModeClass = computed(() => {
    const mode = this.readerSettings().readerMode;
    return mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none';
  });

  protected readonly isValid = computed(() => this.layoutMode() === LayoutMode.DoubleNoCover);

  /**
   * Determines if we should render a double page.
   * The general gist is if we are on double layout mode, the current page (first page) is not a cover image or a wide image
   * and the next page is not a wide image (as only non-wides should be shown next to each other).
   * @remarks This will always fail if the window's width is greater than the height
   */
  protected readonly shouldRenderDouble = computed(() => this.calculateShouldRenderDouble());

  protected readonly layoutClass = computed(() => {
    if (!this.shouldRenderDouble()) return '';

    switch (this.readerSettings().fitting) {
      case FITTING_OPTION.WIDTH: return 'fit-to-width-double-offset';
      case FITTING_OPTION.HEIGHT: return 'fit-to-height-double-offset';
      case FITTING_OPTION.ORIGINAL: return 'original-double-offset';
      default: return '';
    }
  });

  /**
   * Used to render a page on the canvas or in the image tag. This Image element is prefetched by the cachedImages buffer.
   * @remarks Used for rendering to screen.
   */
  currentImage = new Image();
   /**
    * Used solely for LayoutMode.Double rendering.
    * @remarks Used for rendering to screen.
    */
  currentImage2 = new Image();

  ngOnInit(): void {
    this.readerSettings = toSignal(this.readerSettings$(), {injector: this.injector, requireSync: true});
    this.showClickOverlay = toSignal(this.showClickOverlay$(), {injector: this.injector, initialValue: false});

    this.pageNum$().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(pageInfo => {
        this.pageNum.set(pageInfo.pageNum);
        this.maxPages.set(pageInfo.maxPages);

        this.currentImage = this.getPage()(pageInfo.pageNum);
        this.currentImage2 = this.getPage()(pageInfo.pageNum + 1);

        this.cdRef.markForCheck();
      })
    ).subscribe();

    this.bookmark$().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(_ => {
        const elements = [];
        const image1 = this.document.querySelector('#image-1');
        if (image1 != null) elements.push(image1);

        const image2 = this.document.querySelector('#image-2');
        if (image2 != null) elements.push(image2);

        this.mangaReaderService.applyBookmarkEffect(elements);
      })
    ).subscribe();
  }

  // Dimensions arrive as images load, so the cached signal can be stale when a render pass asks
  calculateShouldRenderDouble() {
    if (!this.isValid()) return false;

    if (this.mangaReaderService.isWidePage(this.pageNum()) ) {
      this.debugLog('Not rendering double as current page is wide image');
      return false;
    }

    if (this.mangaReaderService.isSecondLastImage(this.pageNum(), this.maxPages())) {
      this.debugLog('Not rendering double as current page is last');
      return false;
    }

    if (this.mangaReaderService.isLastImage(this.pageNum(), this.maxPages())) {
      this.debugLog('Not rendering double as current page is last');
      return false;
    }

    if (this.mangaReaderService.isWidePage(this.pageNum() + 1) ) {
      this.debugLog('Not rendering double as next page is wide image');
      return false;
    }

    return true;
  }

  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.isValid()) return;

    // First load, switching from double manga -> double, this is 0 and thus not rendering
    if (!this.calculateShouldRenderDouble() && (this.currentImage.height || img[0].height) > 0) {
      return;
    }

    this.cdRef.markForCheck();
  }

  getPageAmount(direction: PAGING_DIRECTION): number {
    if (!this.isValid()) return 0;

    const pageNum = this.pageNum();
    const maxPages = this.maxPages();

    switch (direction) {
      case PAGING_DIRECTION.FORWARD:
        if (this.mangaReaderService.isWidePage(pageNum)) {
          this.debugLog('Moving forward 1 page as current page is wide');
          return 1;
        }
        if (this.mangaReaderService.isWidePage(pageNum + 1)) {
          this.debugLog('Moving forward 1 page as next page is wide');
          return 1;
        }
        if (this.mangaReaderService.isCoverImage(pageNum)) {
          this.debugLog('Moving forward 2 page as on cover image');
          return 2;
        }
        if (this.mangaReaderService.isSecondLastImage(pageNum, maxPages)) {
          this.debugLog('Moving forward 1 page as 2 pages left');
          return 1;
        }
        if (this.mangaReaderService.isLastImage(pageNum, maxPages)) {
          this.debugLog('Moving forward 1 page as 1 page left');
          return 1;
        }
        this.debugLog('Moving forward 2 pages');
        return 2;
      case PAGING_DIRECTION.BACKWARDS:

      if (this.mangaReaderService.isCoverImage(pageNum - 1)) {
        // TODO: If we are moving back and prev page is cover and we are not showing on right side, then move back twice as if we did once, we would show pageNum twice
        this.debugLog('Moving back 1 page as on cover image');
        return 2;
      }

        if (this.mangaReaderService.isCoverImage(pageNum)) {
          this.debugLog('Moving back 1 page as on cover image');
          return 2;
        }

        if (this.mangaReaderService.adjustForDoubleReader(pageNum - 1) != pageNum - 1 && !this.mangaReaderService.isWidePage(pageNum - 2)) {
          this.debugLog('Moving back 2 pages as previous pair should be in a pair');
          return 2;
        }

        if (this.mangaReaderService.isWidePage(pageNum)) {
          this.debugLog('Moving back 1 page as current page is wide');
          return 1;
        }

        if (this.mangaReaderService.isWidePage(pageNum - 1)) {
          this.debugLog('Moving back 1 page as prev page is wide');
          return 1;
        }
        if (this.mangaReaderService.isWidePage(pageNum - 2)) {
          this.debugLog('Moving back 1 page as 2 pages back is wide');
          return 1;
        }

        this.debugLog('Moving back 2 pages');
        return 2;
    }
  }
  reset(): void {}

  getBookmarkPageCount(): number {
    return this.calculateShouldRenderDouble() ? 2 : 1;
  }

  debugLog(message: string, extraData?: any) {
    if (!(this.debugMode & DEBUG_MODES.Logs)) return;

    if (extraData !== undefined) {
      console.log(message, extraData);
    } else {
      console.log(message);
    }
  }
}
