import { DOCUMENT, NgIf, AsyncPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {combineLatest, filter, map, Observable, of, shareReplay, switchMap, tap} from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';
import { LayoutMode } from '../../_models/layout-mode';
import { FITTING_OPTION, PAGING_DIRECTION } from '../../_models/reader-enums';
import { ReaderSetting } from '../../_models/reader-setting';
import { ImageRenderer } from '../../_models/renderer';
import { MangaReaderService } from '../../_service/manga-reader.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SafeStylePipe } from '../../../_pipes/safe-style.pipe';

@Component({
    selector: 'app-single-renderer',
    templateUrl: './single-renderer.component.html',
    styleUrls: ['./single-renderer.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [AsyncPipe, SafeStylePipe]
})
export class SingleRendererComponent implements OnInit, ImageRenderer {

  @Input({required: true}) readerSettings$!: Observable<ReaderSetting>;
  @Input({required: true}) image$!: Observable<HTMLImageElement | null>;
  @Input({required: true}) bookmark$!: Observable<number>;
  @Input({required: true}) showClickOverlay$!: Observable<boolean>;
  @Input({required: true}) pageNum$!: Observable<{pageNum: number, maxPages: number}>;

  @Output() imageHeight: EventEmitter<number> = new EventEmitter<number>();
  private readonly destroyRef = inject(DestroyRef);

  imageFitClass$!: Observable<string>;
  imageContainerHeight$!: Observable<string>;
  showClickOverlayClass$!: Observable<string>;
  readerModeClass$!: Observable<string>;
  darkness$: Observable<string> = of('brightness(100%)');
  emulateBookClass$!: Observable<string>;
  currentImage!: HTMLImageElement;
  layoutMode: LayoutMode = LayoutMode.Single;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;

  pageNum: number = 0;
  maxPages: number = 1;

  /**
   * Width override for maunal width control
  */
  widthOverride$ : Observable<string> = new Observable<string>();

  get ReaderMode() {return ReaderMode;}
  get LayoutMode() {return LayoutMode;}

  constructor(private readonly cdRef: ChangeDetectorRef, public mangaReaderService: MangaReaderService,
    @Inject(DOCUMENT) private document: Document) { }

  ngOnInit(): void {
    this.readerModeClass$ = this.readerSettings$.pipe(
      map(values => values.readerMode),
      map(mode => mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none'),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef)
    );

    //handle manual width
    this.widthOverride$ = this.readerSettings$.pipe(
      map(values => (parseInt(values.widthSlider) <= 0) ? '' : values.widthSlider + '%'),
      takeUntilDestroyed(this.destroyRef)
    );


    this.emulateBookClass$ = this.readerSettings$.pipe(
      map(data => data.emulateBook),
      map(enabled => enabled ? 'book-shadow' : ''),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef)
    );

    this.imageContainerHeight$ = this.image$.pipe(
      filter(_ => this.isValid()),
      switchMap(img => {
        this.cdRef.markForCheck();
        return this.calculateImageContainerHeight$();
      }),
      takeUntilDestroyed(this.destroyRef)
    );


    this.pageNum$.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(pageInfo => {
        this.pageNum = pageInfo.pageNum;
        this.maxPages = pageInfo.maxPages;
      }),
    ).subscribe(() => {});

    this.darkness$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef)
    );

    this.showClickOverlayClass$ = this.showClickOverlay$.pipe(
      map(showOverlay => showOverlay ? 'blur' : ''),
      takeUntilDestroyed(this.destroyRef),
      filter(_ => this.isValid()),
    );

    this.readerSettings$.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(values => {
        this.layoutMode = values.layoutMode;
        this.pageSplit = values.pageSplit;
        this.cdRef.markForCheck();
      })
    ).subscribe(() => {});

    this.bookmark$.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(_ => {
        const elements = [];
        const image1 = this.document.querySelector('#image-1');
        if (image1 != null) elements.push(image1);
        this.mangaReaderService.applyBookmarkEffect(elements);
      }),
      filter(_ => this.isValid()),
    ).subscribe(() => {});

    this.imageFitClass$ = combineLatest([this.readerSettings$, this.pageNum$]).pipe(
      map(values => values[0].fitting),
      map(fit => {
        if (
          this.mangaReaderService.isWidePage(this.pageNum) &&
          this.mangaReaderService.shouldRenderAsFitSplit(this.pageSplit)
          ) {
          // Rewriting to fit to width for this cover image
          return FITTING_OPTION.WIDTH + ' fit-to-screen wide';
        }

        return fit;
      }),
      shareReplay({refCount: true, bufferSize: 1}),
      filter(_ => this.isValid()),
      takeUntilDestroyed(this.destroyRef),
    );
  }

  private calculateImageContainerHeight$(): Observable<string> {
    return this.readerSettings$.pipe(
      map(values => values.fitting),
      map(mode => {
        if (mode !== FITTING_OPTION.HEIGHT) return '';

        const readingArea = this.document.querySelector('.reading-area');
        if (!readingArea) return 'calc(100dvh)';

        // If you ever see fit to height and a bit of scrollbar, it's due to currentImage not being ready on first load
        if (this.currentImage?.width - readingArea.scrollWidth > 0) {
          // we also need to check if this is FF or Chrome. FF doesn't require the -34px as it doesn't render a scrollbar
          return 'calc(100dvh)';
        }
        return 'calc(100dvh)';
      }),
      filter(_ => this.isValid())
    );
  }

  isValid() {
    return this.layoutMode === LayoutMode.Single;
  }

  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (!this.isValid()) return;

    this.currentImage = img[0];
    this.cdRef.markForCheck();
    this.imageHeight.emit(this.currentImage.height);
  }

  shouldMovePrev(): boolean {
    return true;
  }
  shouldMoveNext(): boolean {
    return true;
  }
  getPageAmount(direction: PAGING_DIRECTION): number {
    if (!this.isValid() || this.mangaReaderService.shouldSplit(this.currentImage, this.pageSplit)) return 0;
    return 1;
  }
  reset(): void {}

  getBookmarkPageCount(): number {
    return 1;
  }
}
