import { ElementRef, Injectable, Renderer2, RendererFactory2 } from '@angular/core';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ScalingOption } from 'src/app/_models/preferences/scaling-option';
import { ReaderService } from 'src/app/_services/reader.service';
import { ChapterInfo } from '../_models/chapter-info';
import { DimensionMap } from '../_models/file-dimension';
import { FITTING_OPTION } from '../_models/reader-enums';
import { BookmarkInfo } from 'src/app/_models/manga-reader/bookmark-info';

@Injectable({
  providedIn: 'root'
})
export class MangaReaderService {

  private pageDimensions: DimensionMap = {};
  private pairs: {[key: number]: number} = {};
  private renderer: Renderer2;

  constructor(rendererFactory: RendererFactory2, private readerService: ReaderService) {
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

  shouldRenderAsFitSplit(pageSplitOption: PageSplitOption) {
    // Some pages aren't cover images but might need fit split renderings
    if (parseInt(pageSplitOption + '', 10) !== PageSplitOption.FitSplit) return false;
    return true;
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

    let webtoonScore = 0;
    pages.forEach(info => {
      const aspectRatio = info.height / info.width;
      let score = 0;

      // Strong webtoon indicator: If aspect ratio is at least 2:1
      if (aspectRatio >= 2) {
        score += 1;
      }

      // Boost score if width is small (≤ 800px, common in webtoons)
      if (info.width <= 750) {
        score += 0.5; // Adjust weight as needed
      }

      webtoonScore += score;
    });


    // If at least 50% of the pages fit the webtoon criteria, switch to Webtoon mode.
    return webtoonScore / pages.length >= 0.5;
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
