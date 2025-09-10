import {inject, Injectable, signal} from '@angular/core';
import {NgbOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {
  LoadPageEvent,
  ViewBookmarkDrawerComponent
} from "../book-reader/_components/_drawers/view-bookmarks-drawer/view-bookmark-drawer.component";
import {ViewTocDrawerComponent} from "../book-reader/_components/_drawers/view-toc-drawer/view-toc-drawer.component";
import {UserBreakpoint, UtilityService} from "../shared/_services/utility.service";
import {
  EpubSettingDrawerComponent,
} from "../book-reader/_components/_drawers/epub-setting-drawer/epub-setting-drawer.component";
import {ReadingProfile} from "../_models/preferences/reading-profiles";
import {PageBookmark} from "../_models/readers/page-bookmark";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {
  AnnotationMode,
  ViewEditAnnotationDrawerComponent
} from "../book-reader/_components/_drawers/view-edit-annotation-drawer/view-edit-annotation-drawer.component";
import {AccountService} from "./account.service";
import {EpubReaderSettingsService} from './epub-reader-settings.service';

/**
 * Responsible for opening the different readers and providing any context needed. Handles closing or keeping a stack of menus open.
 */
@Injectable({
  providedIn: 'root'
})
export class EpubReaderMenuService {

  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly utilityService = inject(UtilityService);
  private readonly accountService = inject(AccountService);

  /**
   * The currently active breakpoint, is {@link UserBreakpoint.Never} until the app has loaded
   */
  public readonly isDrawerOpen = signal<boolean>(false);

  openCreateAnnotationDrawer(annotation: Annotation, callbackFn: () => void) {
    const ref = this.offcanvasService.open(ViewEditAnnotationDrawerComponent, {position: 'bottom'});
    ref.closed.subscribe(() => {this.setDrawerClosed(); callbackFn();});
    ref.dismissed.subscribe(() => {this.setDrawerClosed(); callbackFn();});
    (ref.componentInstance as ViewEditAnnotationDrawerComponent).annotation.set(annotation);
    (ref.componentInstance as ViewEditAnnotationDrawerComponent).mode.set(AnnotationMode.Create);

    this.isDrawerOpen.set(true);

    // Set CSS variable for drawer height
    setTimeout(() => {
      var drawerElement = document.querySelector('view-edit-annotation-drawer, app-view-edit-annotation-drawer');
      if (!drawerElement) return;
      var setDrawerHeightVar = function() {
        if (!drawerElement) return;
        var height = (drawerElement as HTMLElement).offsetHeight;
        document.documentElement.style.setProperty('--drawer-height', height + 'px');
      };
      setDrawerHeightVar();
      var resizeObserver = new window.ResizeObserver(function() {
        setDrawerHeightVar();
      });
      resizeObserver.observe(drawerElement as HTMLElement);
      // Optionally store observer for cleanup if needed
    }, 0);
  }


  async openViewAnnotationsDrawer(loadAnnotationCallback: (annotation: Annotation) => void) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }

    // This component needs to be imported dynamically as something breaks within Angular if it's not.
    // I do not know what, but this fixes the drawer from not showing up in a production build.
    const module = await import('../book-reader/_components/_drawers/view-annotations-drawer/view-annotations-drawer.component');
    const ViewAnnotationsDrawerComponent = module.ViewAnnotationsDrawerComponent;

    const ref = this.offcanvasService.open(ViewAnnotationsDrawerComponent, {position: 'end'});
    ref.componentInstance.loadAnnotation.subscribe((annotation: Annotation) => {
      loadAnnotationCallback(annotation);
    });

    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);
  }

  openViewTocDrawer(chapterId: number, pageNum: number, callbackFn: (evt: LoadPageEvent | null) => void) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewTocDrawerComponent, {position: 'end'});
    ref.componentInstance.chapterId.set(chapterId);
    ref.componentInstance.pageNum.set(pageNum);
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

  openViewBookmarksDrawer(chapterId: number,
                          pageNum: number,
                          callbackFn: (evt: PageBookmark | null, action: 'loadPage' | 'removeBookmark') => void,
                          loadPtocCallbackFn: (evt: LoadPageEvent) => void) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(ViewBookmarkDrawerComponent, {position: 'end', panelClass: ''});
    ref.componentInstance.chapterId.set(chapterId);
    ref.componentInstance.pageNum.set(pageNum);
    ref.componentInstance.loadPage.subscribe((res: PageBookmark | null) => {
      // Check if we are on mobile to collapse the menu
      if (this.utilityService.activeUserBreakpoint() <= UserBreakpoint.Mobile) {
        this.closeAll();
      }
      callbackFn(res, 'loadPage');
    });
    ref.componentInstance.loadPtoc.subscribe((res: LoadPageEvent) => {
      // Check if we are on mobile to collapse the menu
      if (this.utilityService.activeUserBreakpoint() <= UserBreakpoint.Mobile) {
        this.closeAll();
      }
      loadPtocCallbackFn(res);
    });
    ref.componentInstance.removeBookmark.subscribe((res: PageBookmark) => {
      // Check if we are on mobile to collapse the menu
      callbackFn(res, 'removeBookmark');
    });
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);

  }


  openSettingsDrawer(chapterId: number, seriesId: number, readingProfile: ReadingProfile, readerSettingsService: EpubReaderSettingsService) {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    const ref = this.offcanvasService.open(EpubSettingDrawerComponent, {position: 'start', panelClass: ''});
    ref.componentInstance.chapterId.set(chapterId);
    ref.componentInstance.seriesId.set(seriesId);
    ref.componentInstance.readingProfile.set(readingProfile);
    ref.componentInstance.readerSettingsService.set(readerSettingsService);

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
    (ref.componentInstance as ViewEditAnnotationDrawerComponent).mode.set(editMode ? AnnotationMode.Edit : AnnotationMode.View);
    ref.closed.subscribe(() => this.setDrawerClosed());
    ref.dismissed.subscribe(() => this.setDrawerClosed());

    this.isDrawerOpen.set(true);

    // Set CSS variable for drawer height
    setTimeout(() => {
      var drawerElement = document.querySelector('view-edit-annotation-drawer, app-view-edit-annotation-drawer');
      if (!drawerElement) return;
      var setDrawerHeightVar = function() {
        if (!drawerElement) return;
        var height = (drawerElement as HTMLElement).offsetHeight;
        document.documentElement.style.setProperty('--drawer-height', height + 'px');
      };
      setDrawerHeightVar();
      var resizeObserver = new window.ResizeObserver(function() {
        setDrawerHeightVar();
      });
      resizeObserver.observe(drawerElement as HTMLElement);
      // Optionally store observer for cleanup if needed
    }, 0);
  }

  closeAll() {
    if (this.offcanvasService.hasOpenOffcanvas()) {
      this.offcanvasService.dismiss();
    }
    this.setDrawerClosed();
  }

  setDrawerClosed() {
    this.isDrawerOpen.set(false);
  }



}
