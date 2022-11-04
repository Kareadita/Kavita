import { DOCUMENT } from '@angular/common';
import { ElementRef, Inject, Injectable, Renderer2, RendererFactory2 } from '@angular/core';
import { PageSplitOption } from 'src/app/_models/preferences/page-split-option';
import { ScalingOption } from 'src/app/_models/preferences/scaling-option';
import { FITTING_OPTION } from '../_models/reader-enums';

@Injectable({
  providedIn: 'root'
})
export class ManagaReaderService {

  private renderer: Renderer2;
  constructor(rendererFactory: RendererFactory2, @Inject(DOCUMENT) private document: Document) {
    this.renderer = rendererFactory.createRenderer(null, null);
  }


  /**
   * If the image's width is greater than it's height
   * @param elem Image
   */
  isWideImage(elem: HTMLImageElement) {
    if (elem) {
      // elem.onload = () => {
      //   return elem.width > elem.height;
      // }
      elem.addEventListener('load', () => {
        return elem.width > elem.height;
      }, false);
      if (elem.src === '') return false;
    }
    const element = elem;
    return element.width > element.height;
  }

  /**
   * If pagenumber is 0 aka first page, which on double page rendering should always render as a single. 
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
    return maxPages - 1 - pageNum === 1;
  }

  /**
   * If the current image is last image
   */
  isLastImage(pageNum: number, maxPages: number) {
    return maxPages - 1 === pageNum;
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


  applyBookmarkEffect(elements: Array<Element | ElementRef>) {
    if (elements.length > 0) {
      elements.forEach(elem => this.renderer.addClass(elem, 'bookmark-effect'));
      setTimeout(() => {
        elements.forEach(elem => this.renderer.removeClass(elem, 'bookmark-effect'));
      }, 1000);
    }
  }




}
