import {inject, Injectable, signal} from '@angular/core';
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
import {
  LoadPageEvent,
  ViewTocDrawerComponent
} from "../book-reader/_components/_drawers/view-toc-drawer/view-toc-drawer.component";
import {UserBreakpoint, UtilityService} from "../shared/_services/utility.service";
import {
  EpubSettingDrawerComponent,
} from "../book-reader/_components/_drawers/epub-setting-drawer/epub-setting-drawer.component";
import {ReadingProfile} from "../_models/preferences/reading-profiles";

/**
 * Responsible for opening the different readers and providing any context needed. Handles closing or keeping a stack of menus open.
 */
@Injectable({
  providedIn: 'root'
})
export class EpubReaderMenuService {

  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly utilityService = inject(UtilityService);

  /**
   * The currently active breakpoint, is {@link UserBreakpoint.Never} until the app has loaded
   */
  public readonly isDrawerOpen = signal<boolean>(false);

  openCreateAnnotationDrawer(annotation: CreateAnnotationRequest) {
    const ref = this.offcanvasService.open(CreateAnnotationDrawerComponent, {position: 'bottom', panelClass: ''});
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());
    ref.componentInstance.createAnnotation.set(annotation);

    this.isDrawerOpen.set(true);
  }


  openViewAnnotationsDrawer(chapterId: number) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewAnnotationDrawerComponent, {position: 'end', panelClass: ''});
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);
  }

  openViewTocDrawer(chapterId: number, callbackFn: (evt: LoadPageEvent | null) => void) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewTocDrawerComponent, {position: 'end', panelClass: ''});
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

  openViewBookmarksDrawer(chapterId: number) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewBookmarkDrawerComponent, {position: 'end', panelClass: ''});
    ref.componentInstance.chapterId.set(chapterId);
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
