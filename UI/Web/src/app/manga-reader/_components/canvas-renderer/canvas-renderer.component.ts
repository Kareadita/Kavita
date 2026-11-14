import {
  AfterViewInit,
  ChangeDetectionStrategy,
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
import {filter, Observable, tap} from 'rxjs';
import {LayoutMode} from '../../_models/layout-mode';
import {FITTING_OPTION, PAGING_DIRECTION, SPLIT_PAGE_PART} from '../../_models/reader-enums';
import {ReaderSetting} from '../../_models/reader-setting';
import {ImageRenderer} from '../../_models/renderer';
import { ImageZoomDirective } from '../../../_directives/image-zoom.directive';
import {MangaReaderService} from '../../_service/manga-reader.service';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {SafeStylePipe} from '../../../_pipes/safe-style.pipe';
import {isSafari} from "../../../_helpers/browser";
import {PageSplitOption} from "../../../_models/preferences/page-split-option";
import {ReaderService} from "../../../_services/reader.service";

const ValidSplits = [PageSplitOption.SplitLeftToRight, PageSplitOption.SplitRightToLeft];

@Component({
    selector: 'app-canvas-renderer',
    templateUrl: './canvas-renderer.component.html',
    styleUrls: ['./canvas-renderer.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [SafeStylePipe, ImageZoomDirective]
})
export class CanvasRendererComponent implements OnInit, AfterViewInit, ImageRenderer {
  private readonly destroyRef = inject(DestroyRef);
  private readonly mangaReaderService = inject(MangaReaderService);
  private readonly readerService = inject(ReaderService);
  private readonly injector = inject(Injector);

  readonly readerSettings$ = input.required<Observable<ReaderSetting>>();
  readonly image$ = input.required<Observable<HTMLImageElement | null>>();
  readonly bookmark$ = input.required<Observable<number>>();

  readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('content');
  private ctx!: CanvasRenderingContext2D;

  currentImageSplitPart: SPLIT_PAGE_PART = SPLIT_PAGE_PART.NO_SPLIT;
  canvasImage: HTMLImageElement | null = null;
  /**
   * This renderer only takes over when a wide page has to be split, so it doubles as the template's visibility flag
   */
  readonly renderWithCanvas = signal(false);

  private readerSettings!: Signal<ReaderSetting>;

  private readonly pageSplit = computed(() => this.readerSettings().pageSplit);
  private readonly layoutMode = computed(() => this.readerSettings().layoutMode);
  private readonly pagingDirection = computed(() => this.readerSettings().pagingDirection);

  protected readonly darkness = computed(() => 'brightness(' + this.readerSettings().darkness + '%)');
  // canvasImage is read untracked on purpose: the class follows settings only, as the stream it replaced did
  protected readonly imageFitClass = computed(() => {
    const fit = this.readerSettings().fitting;
    if (fit === FITTING_OPTION.WIDTH) return fit; // || this.layoutMode === LayoutMode.Single (so that we can check the wide stuff)
    if (this.canvasImage === null) return fit;

    // Would this ever execute given that we perform splitting only in this renderer?
    if (
      this.mangaReaderService.isWidePage(this.readerService.imageUrlToPageNum(this.canvasImage.src)) &&
      this.mangaReaderService.shouldRenderAsFitSplit(this.pageSplit())
      ) {
      // Rewriting to fit to width for this cover image
      return FITTING_OPTION.WIDTH;
    }
    return fit;
  });

  ngOnInit(): void {
    this.readerSettings = toSignal(this.readerSettings$(), {injector: this.injector, requireSync: true});

    // The reset has to land before the re-render that follows a settings change, so this stays a subscription
    let previousPageSplit = this.pageSplit();
    this.readerSettings$().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap((value: ReaderSetting) => {
        const rerenderNeeded = previousPageSplit !== value.pageSplit;
        previousPageSplit = value.pageSplit;
        if (rerenderNeeded) {
          this.reset();
        }
      })
    ).subscribe();

    this.bookmark$().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(_ => {
        if (this.currentImageSplitPart === SPLIT_PAGE_PART.NO_SPLIT) return;
        const canvas = this.canvas();
        if (!canvas) return;

        const elements = [canvas?.nativeElement];
        this.mangaReaderService.applyBookmarkEffect(elements);
      })
    ).subscribe();

    // This is needed in case the reader loads on the canvas renderer and first render has a width of 0 from image not loading fully
    this.image$().pipe(
      takeUntilDestroyed(this.destroyRef),
      filter(img => img !== null && img === this.canvasImage),
      filter(() => this.renderWithCanvas() && this.currentImageSplitPart !== SPLIT_PAGE_PART.NO_SPLIT),
      tap(() => this.drawSplitPage())
    ).subscribe();
  }

  ngAfterViewInit() {
    const canvas = this.canvas();
    if (canvas) {
      this.ctx = canvas.nativeElement.getContext('2d', { alpha: false })!;
    }
  }


  reset() {
    this.currentImageSplitPart = SPLIT_PAGE_PART.NO_SPLIT;
  }

  updateSplitPage() {
    if (this.canvasImage == null) return;
    const needsSplitting = this.mangaReaderService.isWidePage(this.readerService.imageUrlToPageNum(this.canvasImage.src));
    const pageSplit = this.pageSplit();

    if (!needsSplitting || this.mangaReaderService.isNoSplit(pageSplit)) {
      this.currentImageSplitPart = SPLIT_PAGE_PART.NO_SPLIT;
      return needsSplitting;
    }
    const splitLeftToRight = this.mangaReaderService.isSplitLeftToRight(pageSplit);

    if (this.pagingDirection() === PAGING_DIRECTION.FORWARD) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          {
            const r2lSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.RIGHT_PART : r2lSplittingPart;
          break;
          }
        case SPLIT_PAGE_PART.RIGHT_PART:
          {
            const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = splitLeftToRight ? l2rSplittingPart : SPLIT_PAGE_PART.LEFT_PART;
          break;
          }
      }
    } else if (this.pagingDirection() === PAGING_DIRECTION.BACKWARDS) {
      switch (this.currentImageSplitPart) {
        case SPLIT_PAGE_PART.NO_SPLIT:
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART;
          break;
        case SPLIT_PAGE_PART.LEFT_PART:
          {
            const l2rSplittingPart = (needsSplitting ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          this.currentImageSplitPart = splitLeftToRight? l2rSplittingPart : SPLIT_PAGE_PART.RIGHT_PART;
          break; 
          }
        case SPLIT_PAGE_PART.RIGHT_PART:
          this.currentImageSplitPart = splitLeftToRight ? SPLIT_PAGE_PART.LEFT_PART : (needsSplitting ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.NO_SPLIT);
          break;
      }
    }
    return needsSplitting;
  }

  /**
   * This renderer does not render when splitting is not needed
   * @param img
   * @returns
   */
  renderPage(img: Array<HTMLImageElement | null>) {
    this.renderWithCanvas.set(false);

    if (img === null || img.length === 0 || img[0] === null) return;

    const canvas = this.canvas();
    if (!this.ctx || !canvas) return;

    this.canvasImage = img[0];

    if (this.layoutMode() !== LayoutMode.Single || !ValidSplits.includes(this.pageSplit())) {
      return;
    }

    const needsSplitting = this.updateSplitPage();
    if (!needsSplitting) return;

    // This is toggling true when manga reader shouldn't use this code

    this.renderWithCanvas.set(true);
    if (this.currentImageSplitPart === SPLIT_PAGE_PART.NO_SPLIT) return;

    this.drawSplitPage();
  }

  /**
   * Paints the half of canvasImage that currentImageSplitPart already points at. This only draws, so it
   * is safe to call again when the image finishes loading, unlike renderPage which advances the split part.
   */
  private drawSplitPage() {
    const canvas = this.canvas();
    if (this.canvasImage === null || !this.ctx || !canvas) return;
    // Nothing to paint from yet; the image$ subscription redraws once it has loaded
    if (this.canvasImage.width === 0 || this.canvasImage.height === 0) return;

    this.setCanvasSize();

    if (this.currentImageSplitPart === SPLIT_PAGE_PART.LEFT_PART) {
      canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, 0, 0, this.canvasImage.width, this.canvasImage.height);
    } else if (this.currentImageSplitPart === SPLIT_PAGE_PART.RIGHT_PART) {
      canvas.nativeElement.width = this.canvasImage.width / 2;
      this.ctx.drawImage(this.canvasImage, 0, 0, this.canvasImage.width, this.canvasImage.height, -this.canvasImage.width / 2, 0, this.canvasImage.width, this.canvasImage.height);
    }
  }

  getPageAmount(direction: PAGING_DIRECTION) {
    if (this.canvasImage === null) return 1;
    if (!this.mangaReaderService.isWidePage(this.readerService.imageUrlToPageNum(this.canvasImage.src))) return 1;
    switch(direction) {
      case PAGING_DIRECTION.FORWARD:
        return this.shouldMoveNext() ? 1 : 0;
      case PAGING_DIRECTION.BACKWARDS:
        return this.shouldMovePrev() ? 1 : 0;
    }
  }

  shouldMoveNext() {
    const pageSplit = this.pageSplit();
    if (this.mangaReaderService.isNoSplit(pageSplit)) return true;
    return this.currentImageSplitPart !== (this.mangaReaderService.isSplitLeftToRight(pageSplit) ? SPLIT_PAGE_PART.LEFT_PART : SPLIT_PAGE_PART.RIGHT_PART);
  }

  shouldMovePrev() {
    const pageSplit = this.pageSplit();
    if (this.mangaReaderService.isNoSplit(pageSplit)) return true;
    return this.currentImageSplitPart !== (this.mangaReaderService.isSplitLeftToRight(pageSplit) ? SPLIT_PAGE_PART.RIGHT_PART : SPLIT_PAGE_PART.LEFT_PART);
  }

  /**
   * There are some hard limits on the size of canvas' that we must cap at. https://github.com/jhildenbiddle/canvas-size#test-results
   * For Safari, it's 16,777,216, so we cap at 4096x4096 when this happens. The drawImage in render will perform bi-cubic scaling for us.
   */
   setCanvasSize() {
    if (this.canvasImage == null) return;
    const canvas = this.canvas();
    if (!this.ctx || !canvas) { return; }
    const canvasLimit = this.isSafari ? 16_777_216 : 124_992_400;
    const needsScaling = this.canvasImage.width * this.canvasImage.height > canvasLimit;
    if (needsScaling) {
      canvas.nativeElement.width = this.isSafari ? 4_096 : 16_384;
      canvas.nativeElement.height = this.isSafari ? 4_096 : 16_384;
    } else {
      canvas.nativeElement.width = this.canvasImage.width;
      canvas.nativeElement.height = this.canvasImage.height;
    }
  }

  getBookmarkPageCount(): number {
    return 1;
  }

  protected readonly isSafari = isSafari;
}
