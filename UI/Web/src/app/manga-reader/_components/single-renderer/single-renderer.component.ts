import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject, Input, OnDestroy, OnInit } from '@angular/core';
import { map, Observable, Subject, takeUntil, tap } from 'rxjs';
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

  readerModeClass$!: Observable<string>;
  currentImage!: HTMLImageElement;
  layoutMode: LayoutMode = LayoutMode.Single;

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
    this.readerSettings$.pipe(
      takeUntil(this.onDestroy),
      tap(values => {
        this.layoutMode = values.layoutMode;
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

    this.image$.pipe(
      takeUntil(this.onDestroy),
      tap(img => {
        if (img != null) {
          this.currentImage = img;
          this.cdRef.markForCheck();
        }
      })
    ).subscribe(() => {});
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }
  
  renderPage(img: Array<HTMLImageElement | null>): void {
    if (img === null || img.length === 0 || img[0] === null) return;
    this.currentImage = img[0];
    if (this.layoutMode !== LayoutMode.Single) return;
    this.cdRef.markForCheck();
  }

  shouldMovePrev(): boolean {
    return true;
  }
  shouldMoveNext(): boolean {
    return true;
  }
  getPageAmount(): number {
    // TODO: Check if in a mode that this renderer is valid, if not return 0
    return 1;
  }
  reset(): void {}
}
