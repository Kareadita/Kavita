import {inject, Injectable, signal} from '@angular/core';
import {CreateAnnotationRequest} from "../book-reader/_models/create-annotation-request";
import {NgbOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {
  ViewAnnotationsDrawerComponent
} from "../book-reader/_components/_drawers/view-annotations-drawer/view-annotations-drawer.component";
import {
  CreateAnnotationDrawerComponent
} from "../book-reader/_components/_drawers/create-annotation-drawer/create-annotation-drawer.component";
import {
  ViewBookmarkDrawerComponent
} from "../book-reader/_components/_drawers/view-bookmarks-drawer/view-bookmark-drawer.component";
import {
  LoadPageEvent,
  ViewTocDrawerComponent
} from "../book-reader/_components/_drawers/view-toc-drawer/view-toc-drawer.component";
import {UserBreakpoint, UtilityService} from "../shared/_services/utility.service";
import {
  EpubSettingDrawerComponent,
} from "../book-reader/_components/_drawers/epub-setting-drawer/epub-setting-drawer.component";
import {ReadingProfile} from "../_models/preferences/reading-profiles";
import {PageBookmark} from "../_models/readers/page-bookmark";
import {Annotation} from "../book-reader/_models/annotation";
import {
  ViewEditAnnotationDrawerComponent
} from "../book-reader/_components/_drawers/view-edit-annotation-drawer/view-edit-annotation-drawer.component";
import {HighlightColorPipe} from "../_pipes/highlight-color.pipe";

/**
 * Responsible for opening the different readers and providing any context needed. Handles closing or keeping a stack of menus open.
 */
@Injectable({
  providedIn: 'root'
})
export class EpubReaderMenuService {

  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly utilityService = inject(UtilityService);
  private readonly highlightColorPipe = new HighlightColorPipe();

  /**
   * The currently active breakpoint, is {@link UserBreakpoint.Never} until the app has loaded
   */
  public readonly isDrawerOpen = signal<boolean>(false);

  openCreateAnnotationDrawer(annotation: CreateAnnotationRequest, callbackFn: () => void) {
    const ref = this.offcanvasService.open(CreateAnnotationDrawerComponent, {position: 'bottom'});
    ref.closed.subscribe(() => {this.setDrawerClosed(); callbackFn();});
    ref.dismissed.subscribe(() => {this.setDrawerClosed(); callbackFn();});
    ref.componentInstance.createAnnotation.set(annotation);

    this.isDrawerOpen.set(true);
  }


  openViewAnnotationsDrawer() {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewAnnotationsDrawerComponent, {position: 'end'});
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);
  }

  openViewTocDrawer(chapterId: number, callbackFn: (evt: LoadPageEvent | null) => void) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewTocDrawerComponent, {position: 'end'});
    ref.componentInstance.chapterId.set(chapterId);
    ref.componentInstance.loadPage.subscribe((res: LoadPageEvent | null) => {
      // Check if we are on mobile to collapse the menu
      if (this.utilityService.activeUserBreakpoint() <= UserBreakpoint.Mobile) {
        this.closeAll();
      }
      callbackFn(res);
    });
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);
  }

  openViewBookmarksDrawer(chapterId: number, callbackFn: (evt: PageBookmark | null) => void) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewBookmarkDrawerComponent, {position: 'end', panelClass: ''});
    ref.componentInstance.chapterId.set(chapterId);
    ref.componentInstance.loadPage.subscribe((res: PageBookmark | null) => {
      // Check if we are on mobile to collapse the menu
      if (this.utilityService.activeUserBreakpoint() <= UserBreakpoint.Mobile) {
        this.closeAll();
      }
      callbackFn(res);
    });
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);

  }


  openSettingsDrawer(chapterId: number, seriesId: number, readingProfile: ReadingProfile) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(EpubSettingDrawerComponent, {position: 'start', panelClass: ''});
    ref.componentInstance.chapterId.set(chapterId);
    ref.componentInstance.seriesId.set(seriesId);
    ref.componentInstance.readingProfile.set(readingProfile);

    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);
  }

  openViewAnnotationDrawer(annotation: Annotation, editMode: boolean = false, callbackFn: (res: Annotation) => void) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }

    if (!editMode && this.utilityService.activeUserBreakpoint() <= UserBreakpoint.Tablet) {
      // Open a modal to view the annotation?
    }

    const ref = this.offcanvasService.open(ViewEditAnnotationDrawerComponent, {position: 'bottom'});
    ref.componentInstance.annotation.set(annotation);
    ref.componentInstance.isEditMode.set(editMode);
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);
  }

  closeAll() {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    this.setDrawerClosed();
  }

  setDrawerClosed() {
    console.log('Drawer closed');
    this.isDrawerOpen.set(false);
  }



}
