import { ElementRef, Injectable, Renderer2, RendererFactory2, inject } from '@angular/core';
import {PageSplitOption} from 'src/app/_models/preferences/page-split-option';
import {ScalingOption} from 'src/app/_models/preferences/scaling-option';
import {ReaderService} from 'src/app/_services/reader.service';
import {ChapterInfo} from '../_models/chapter-info';
import {DimensionMap} from '../_models/file-dimension';
import {FITTING_OPTION} from '../_models/reader-enums';
import {BookmarkInfo} from 'src/app/_models/manga-reader/bookmark-info';

@Injectable({
  providedIn: 'root'
})
export class MangaReaderService {
  private readerService = inject(ReaderService);


  private pageDimensions: DimensionMap = {};
  private pairs: {[key: number]: number} = {};
  private renderer: Renderer2;

  constructor() {
    const rendererFactory = inject(RendererFactory2);

    this.renderer = rendererFactory.createRenderer(null, null);
  }

  load(chapterInfo: ChapterInfo | BookmarkInfo) {
    chapterInfo.pageDimensions!.forEach(d => {
      this.pageDimensions[d.pageNumber] = {
        height: d.height,
        width: d.width,
        isWide: d.isWide
      };
    });
    this.pairs = chapterInfo.doublePairs!;
  }

  adjustForDoubleReader(page: number) {
    if (!this.pairs.hasOwnProperty(page)) return page;
    return this.pairs[page];
  }

  getPageDimensions(pageNum: number) {
    if (!this.pageDimensions.hasOwnProperty(pageNum)) return null;
    return this.pageDimensions[pageNum];
  }

  /**
   * If the image's width is greater than it's height
   * @param pageNum Page number - Expected to call loadPageDimensions before this call
   */
  isWidePage(pageNum: number) {
    if (!this.pageDimensions.hasOwnProperty(pageNum)) return false;
    return this.pageDimensions[pageNum].isWide;
  }

  maxHeight() {
    return  Object.values(this.pageDimensions).reduce((max, obj) => Math.max(max, obj.height), 0);
  }

  maxWidth() {
    return  Object.values(this.pageDimensions).reduce((max, obj) => Math.max(max, obj.width), 0);
  }



  /**
   * If pageNumber is 0 aka first page, which on double page rendering should always render as a single.
   *
   * @param pageNumber current page number
   * @returns
   */
  isCoverImage(pageNumber: number) {
    return pageNumber === 0;
  }

  /**
   * Does the image need
   * @returns If the current model reflects no split of fit split
   * @remarks Fit to Screen falls under no split
   */
  isNoSplit(pageSplitOption: PageSplitOption) {
    const splitValue = parseInt(pageSplitOption + '', 10); // Just in case it's a string from form
    return splitValue === PageSplitOption.NoSplit || splitValue === PageSplitOption.FitSplit;
  }

  /**
   * If the split option is Left to Right. This means that the Left side of the image renders before the Right side.
   * In other words, If you were to visualize the parts as pages, Left is Page 0, Right is Page 1
   */
  isSplitLeftToRight(pageSplitOption: PageSplitOption) {
    return parseInt(pageSplitOption + '', 10) === PageSplitOption.SplitLeftToRight;
  }

  /**
   * If the current page is second to last image
   */
  isSecondLastImage(pageNum: number, maxPages: number) {
    return (maxPages - 2) === pageNum;
  }

  /**
   * If the current image is last image
   */
  isLastImage(pageNum: number, maxPages: number) {
    return maxPages - 1 === pageNum;
  }

  /**
   * Should Canvas Renderer be used
   * @param img
   * @param pageSplitOption
   * @returns
   */
  shouldSplit(img: HTMLImageElement, pageSplitOption: PageSplitOption) {
    const needsSplitting = this.isWidePage(this.readerService.imageUrlToPageNum(img?.src));
    return !(this.isNoSplit(pageSplitOption) || !needsSplitting)
  }

  /**
   * Some pages aren't cover images but might need fit split renderings
   * @param pageSplitOption
   */
  shouldRenderAsFitSplit(pageSplitOption: PageSplitOption) {
    return parseInt(pageSplitOption + '', 10) === PageSplitOption.FitSplit;
  }


  translateScalingOption(option: ScalingOption) {
    switch (option) {
      case (ScalingOption.Automatic):
      {
        const windowWidth = window.innerWidth
                  || document.documentElement.clientWidth
                  || document.body.clientWidth;
        const windowHeight = window.innerHeight
                  || document.documentElement.clientHeight
                  || document.body.clientHeight;

        const ratio = windowWidth / windowHeight;
        if (windowHeight > windowWidth) {
          return FITTING_OPTION.WIDTH;
        }

        if (windowWidth >= windowHeight || ratio > 1.0) {
          return FITTING_OPTION.HEIGHT;
        }
        return FITTING_OPTION.WIDTH;
      }
      case (ScalingOption.FitToHeight):
        return FITTING_OPTION.HEIGHT;
      case (ScalingOption.FitToWidth):
        return FITTING_OPTION.WIDTH;
      default:
        return FITTING_OPTION.ORIGINAL;
    }
  }

  /**
   * If the page dimensions are all "webtoon-like", then reader mode will be converted for the user
   */
  shouldBeWebtoonMode() {
    const pages = Object.values(this.pageDimensions);

    // Require a minimum number of pages for reliable detection
    if (pages.length < 3) {
      return false;
    }

    // Get statistical properties across all pages
    const aspectRatios = pages.map(info => info.height / info.width);
    const avgAspectRatio = aspectRatios.reduce((sum, ratio) => sum + ratio, 0) / pages.length;
    const stdDevAspectRatio = Math.sqrt(
      aspectRatios.reduce((sum, ratio) => sum + Math.pow(ratio - avgAspectRatio, 2), 0) / pages.length
    );

    // Consider page dimensions consistency
    const widths = pages.map(info => info.width);
    const heights = pages.map(info => info.height);
    const avgWidth = widths.reduce((sum, w) => sum + w, 0) / pages.length;
    const avgHeight = heights.reduce((sum, h) => sum + h, 0) / pages.length;

    // Calculate variation coefficients for width and height
    const widthVariation = Math.sqrt(
      widths.reduce((sum, w) => sum + Math.pow(w - avgWidth, 2), 0) / pages.length
    ) / avgWidth;

    // Calculate individual scores for each page
    let webtoonScore = 0;
    let strongIndicatorCount = 0;

    pages.forEach(info => {
      const aspectRatio = info.height / info.width;
      let score = 0;

      // Strong webtoon indicator: If aspect ratio is at least 2:1
      if (aspectRatio >= 2.2) {
        score += 1;
        strongIndicatorCount++;
      } else if (aspectRatio >= 1.8 && aspectRatio < 2.2) {
        // Moderate indicator
        score += 0.5;
      } else if (aspectRatio >= 1.5 && aspectRatio < 1.8) {
        // Weak indicator - many regular manga/comics have ratios in this range
        score += 0.2;
      }

      // Penalize pages that are too square-like (common in traditional comics)
      if (aspectRatio < 1.2) {
        score -= 0.5;
      }

      // Consider width but with less weight than before
      if (info.width <= 750) {
        score += 0.2;
      }

      // Consider absolute height (long strips tend to be very tall)
      if (info.height > 2000) {
        score += 0.5;
      } else if (info.height > 1500) {
        score += 0.3;
      }

      // Consider absolute page area - webtoons tend to have larger total area
      const area = info.width * info.height;
      if (area > 1500000) { // e.g., 1000×1500 or larger
        score += 0.3;
      }

      webtoonScore += score;
    });

    const averageScore = webtoonScore / pages.length;

    // Multiple criteria for more robust detection
    // Check for typical manga/comic dimensions that should NOT be webtoon mode
    const isMangaLikeSize = avgHeight < 1200 && avgAspectRatio < 1.7 && avgWidth < 700;

    // Main detection criteria
    return (
      // Primary criterion: average score threshold (increased)
      averageScore >= 0.7 &&
      // Not resembling typical manga/comic dimensions
      !isMangaLikeSize &&
      // Secondary criteria (any one can satisfy)
      (
        // Most pages should have high aspect ratio
        (strongIndicatorCount / pages.length >= 0.4) ||
        // Average aspect ratio is high enough (increased threshold)
        (avgAspectRatio >= 2.0) ||
        // Pages have consistent width AND very high aspect ratio
        (widthVariation < 0.15 && avgAspectRatio > 1.8)
      )
    );
  }


  applyBookmarkEffect(elements: Array<Element | ElementRef>) {
    if (elements.length > 0) {
      elements.forEach(elem => this.renderer.addClass(elem, 'bookmark-effect'));
      setTimeout(() => {
        elements.forEach(elem => this.renderer.removeClass(elem, 'bookmark-effect'));
      }, 1000);
    }
  }

}
