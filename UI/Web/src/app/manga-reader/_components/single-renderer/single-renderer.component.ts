import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Inject, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { combineLatest, filter, map, Observable, of, shareReplay, Subject, takeUntil, tap } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';
import { ReaderService } from 'src/app/_services/reader.service';
import { LayoutMode } from '../../_models/layout-mode';
import { FITTING_OPTION, PAGING_DIRECTION } from '../../_models/reader-enums';
import { ReaderSetting } from '../../_models/reader-setting';
import { ImageRenderer } from '../../_models/renderer';
import { ManagaReaderService } from '../../_series/managa-reader.service';

@Component({
  selector: 'app-single-renderer',
  templateUrl: './single-renderer.component.html',
  styleUrls: ['./single-renderer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SingleRendererComponent implements OnInit, OnDestroy, ImageRenderer {

  @Input() readerSettings$!: Observable<ReaderSetting>;
  @Input() image$!: Observable<HTMLImageElement | null>;
  @Input() bookmark$!: Observable<number>;
  @Input() showClickOverlay$!: Observable<boolean>;
  @Input() pageNum$!: Observable<{pageNum: number, maxPages: number}>;

  @Output() imageHeight: EventEmitter<number> = new EventEmitter<number>();

  imageFitClass$!: Observable<string>;
  imageContainerHeight$!: Observable<string>;
  showClickOverlayClass$!: Observable<string>;
  readerModeClass$!: Observable<string>;
  darkenss$: Observable<string> = of('brightness(100%)');
  emulateBookClass$!: Observable<string>;
  currentImage!: HTMLImageElement;
  layoutMode: LayoutMode = LayoutMode.Single;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;

  pageNum: number = 0;
  maxPages: number = 1;

  private readonly onDestroy = new Subject<void>();

  get ReaderMode() {return ReaderMode;} 
  get LayoutMode() {return LayoutMode;} 

  constructor(private readonly cdRef: ChangeDetectorRef, public mangaReaderService: ManagaReaderService, 
    @Inject(DOCUMENT) private document: Document) { }

  ngOnInit(): void {
    this.readerModeClass$ = this.readerSettings$.pipe(
      map(values => values.readerMode), 
      map(mode => mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none'),
      filter(_ => this.isValid()),
      takeUntil(this.onDestroy)
    );

    this.emulateBookClass$ = this.readerSettings$.pipe(
      map(data => data.emulateBook),
      map(enabled => enabled ? 'book-shadow' : ''), 
      filter(_ => this.isValid()),
      takeUntil(this.onDestroy)
    );

    this.imageContainerHeight$ = this.readerSettings$.pipe(
      map(values => values.fitting), 
      map(mode => {
        if ( mode !== FITTING_OPTION.HEIGHT) return '';

        const readingArea = this.document.querySelector('.reading-area');
        if (!readingArea) return 'calc(100vh)';

        if (this.currentImage.width - readingArea.scrollWidth > 0) {
          return 'calc(100vh - 34px)'
        }
        return 'calc(100vh)'
      }),
      filter(_ => this.isValid()),
      takeUntil(this.onDestroy)
    );

    this.pageNum$.pipe(
      takeUntil(this.onDestroy),
      tap(pageInfo => {
        this.pageNum = pageInfo.pageNum;
        this.maxPages = pageInfo.maxPages;
      }),
    ).subscribe(() => {});

    this.darkenss$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'), 
      filter(_ => this.isValid()),
      takeUntil(this.onDestroy)
    );

    this.showClickOverlayClass$ = this.showClickOverlay$.pipe(
      map(showOverlay => showOverlay ? 'blur' : ''), 
      takeUntil(this.onDestroy),
      filter(_ => this.isValid()),
    );

    this.readerSettings$.pipe(
      takeUntil(this.onDestroy),
      tap(values => {
        this.layoutMode = values.layoutMode;
        this.pageSplit = values.pageSplit;
        this.cdRef.markForCheck();
      })
    ).subscribe(() => {});

    this.bookmark$.pipe(
      takeUntil(this.onDestroy),
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
      shareReplay(),
      filter(_ => this.isValid()),
      takeUntil(this.onDestroy),
    );
  }

  isValid() {
    return this.layoutMode === LayoutMode.Single;
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
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
