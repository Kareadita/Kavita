import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Inject, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { map, Observable, of, Subject, takeUntil, tap } from 'rxjs';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';
import { LayoutMode } from '../../_models/layout-mode';
import { FITTING_OPTION } from '../../_models/reader-enums';
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
  /**
   * The image fit class
   */
  @Input() imageFit$!: Observable<FITTING_OPTION>;  
  @Input() bookmark$!: Observable<number>;
  @Input() showClickOverlay$!: Observable<boolean>;

  @Output() imageHeight: EventEmitter<number> = new EventEmitter<number>();

  imageFitClass$!: Observable<string>;
  showClickOverlayClass$!: Observable<string>;
  readerModeClass$!: Observable<string>;
  darkenss$: Observable<string> = of('brightness(100%)');
  currentImage!: HTMLImageElement;
  layoutMode: LayoutMode = LayoutMode.Single;
  pageSplit: PageSplitOption = PageSplitOption.FitSplit;

  private readonly onDestroy = new Subject<void>();

  get ReaderMode() {return ReaderMode;} 

  constructor(private readonly cdRef: ChangeDetectorRef, private mangaReaderService: ManagaReaderService, 
    @Inject(DOCUMENT) private document: Document) { }

  ngOnInit(): void {
    this.readerModeClass$ = this.readerSettings$.pipe(
      map(values => values.readerMode), 
      map(mode => mode === ReaderMode.LeftRight || mode === ReaderMode.UpDown ? '' : 'd-none'),
      takeUntil(this.onDestroy)
    );

    this.darkenss$ = this.readerSettings$.pipe(
      map(values => 'brightness(' + values.darkness + '%)'), 
      takeUntil(this.onDestroy)
    );

    this.showClickOverlayClass$ = this.showClickOverlay$.pipe(
      map(showOverlay => showOverlay ? 'blur' : ''), 
      takeUntil(this.onDestroy)
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
        console.log('Applying bookmark on ', image1);
        this.mangaReaderService.applyBookmarkEffect(elements);
      })
    ).subscribe(() => {});

    // this.imageFitClass$ = this.imageFit$.pipe(
    //   takeUntil(this.onDestroy),
    //   map(fit => {
    //     if (
    //       this.mangaReaderService.isWideImage(this.currentImage) &&
    //       this.layoutMode === LayoutMode.Single &&
    //       fit !== FITTING_OPTION.WIDTH &&
    //       this.mangaReaderService.shouldRenderAsFitSplit(this.pageSplit)
    //       ) {
    //       // Rewriting to fit to width for this cover image
    //       return FITTING_OPTION.WIDTH;
    //     }
    //     return fit;
    //   })
    // );

    this.imageFitClass$ = this.readerSettings$.pipe(
      takeUntil(this.onDestroy),
      map(values => values.fitting),
      map(fit => {
        if (
          this.mangaReaderService.isWideImage(this.currentImage) &&
          this.layoutMode === LayoutMode.Single &&
          fit !== FITTING_OPTION.WIDTH &&
          this.mangaReaderService.shouldRenderAsFitSplit(this.pageSplit)
          ) {
          // Rewriting to fit to width for this cover image
          return FITTING_OPTION.WIDTH;
        }
        return fit;
      })
    );
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }
  
  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    if (this.layoutMode !== LayoutMode.Single) return;
    if (this.mangaReaderService.shouldSplit(this.currentImage, this.pageSplit)) return;

    this.currentImage = img[0];
    this.imageHeight.emit(this.currentImage.height);
    this.cdRef.markForCheck();
  }

  shouldMovePrev(): boolean {
    return true;
  }
  shouldMoveNext(): boolean {
    return true;
  }
  getPageAmount(): number {
    if (this.layoutMode !== LayoutMode.Single || this.mangaReaderService.shouldSplit(this.currentImage, this.pageSplit)) return 0;
    return 1;
  }
  reset(): void {}
}
