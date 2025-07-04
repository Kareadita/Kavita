import {inject, Injectable} from '@angular/core';
import {CreateAnnotationRequest} from "../book-reader/_models/create-annotation-request";
import {NgbOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {
  ViewAnnotationDrawerComponent
} from "../book-reader/_components/_drawers/view-annotation-drawer/view-annotation-drawer.component";
import {
  CreateAnnotationDrawerComponent
} from "../book-reader/_components/_drawers/create-annotation-drawer/create-annotation-drawer.component";
import {
  ViewBookmarkDrawerComponent
} from "../book-reader/_components/_drawers/view-bookmarks-drawer/view-bookmark-drawer.component";
import {ActivatedRoute} from "@angular/router";
import {ViewTocDrawerComponent} from "../book-reader/_components/_drawers/view-toc-drawer/view-toc-drawer.component";

/**
 * Responsible for opening the different readers and providing any context needed. Handles closing or keeping a stack of menus open.
 */
@Injectable({
  providedIn: 'root'
})
export class EpubReaderMenuService {

  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly route = inject(ActivatedRoute);


  openCreateAnnotationDrawer(annotation: CreateAnnotationRequest) {
    const ref = this.offcanvasService.open(CreateAnnotationDrawerComponent, {position: 'bottom', panelClass: ''});
    ref.componentInstance.createAnnotation.set(annotation)
  }


  openViewAnnotationsDrawer(chapterId: number) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewAnnotationDrawerComponent, {position: 'end', panelClass: ''});
  }

  openTocDrawer() {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewTocDrawerComponent, {position: 'end', panelClass: ''});
  }

  openViewBookmarksDrawer(chapterId: number) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewBookmarkDrawerComponent, {position: 'end', panelClass: ''});
    ref.componentInstance.chapterId.set(chapterId);

  }

  closeAll() {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
  }



}
