import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { Subject } from 'rxjs';
import { filter, map, shareReplay, take, takeUntil } from 'rxjs/operators';
import { ImportCblModalComponent } from 'src/app/reading-list/_modals/import-cbl-modal/import-cbl-modal.component';
import { ReadingList } from 'src/app/_models/reading-list';
import { ImageService } from 'src/app/_services/image.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { Breakpoint, UtilityService } from '../../../shared/_services/utility.service';
import { Library, LibraryType } from '../../../_models/library';
import { AccountService } from '../../../_services/account.service';
import { Action, ActionFactoryService, ActionItem } from '../../../_services/action-factory.service';
import { ActionService } from '../../../_services/action.service';
import { LibraryService } from '../../../_services/library.service';
import { NavService } from '../../../_services/nav.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-side-nav',
  templateUrl: './side-nav.component.html',
  styleUrls: ['./side-nav.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  libraries: Library[] = [];
  actions: ActionItem<Library>[] = [];
  readingListActions = [{action: Action.Import, title: 'Import CBL', children: [], requiresAdmin: true, callback: this.importCbl.bind(this)}];

  filterQuery: string = '';
  filterLibrary = (library: Library) => {
    return library.name.toLowerCase().indexOf((this.filterQuery || '').toLowerCase()) >= 0;
  }


  constructor(public accountService: AccountService, private libraryService: LibraryService,
    public utilityService: UtilityService, private messageHub: MessageHubService,
    private actionFactoryService: ActionFactoryService, private actionService: ActionService,
    public navService: NavService, private router: Router, private readonly cdRef: ChangeDetectorRef,
    private ngbModal: NgbModal, private imageService: ImageService) {

      this.router.events.pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
        map(evt => evt as NavigationEnd),
        filter(() => this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet))
      .subscribe((evt: NavigationEnd) => {
        // Collapse side nav on mobile
        this.navService.sideNavCollapsed$.pipe(take(1)).subscribe(collapsed => {
          if (!collapsed) {
            this.navService.toggleSideNav();
            this.cdRef.markForCheck();
          }
        });
      });
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (!user) return;
      this.libraryService.getLibraries().pipe(take(1), shareReplay()).subscribe((libraries: Library[]) => {
        this.libraries = libraries;
        this.cdRef.markForCheck();
      });
      this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
      this.cdRef.markForCheck();
    });

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), filter(event => event.event === EVENTS.LibraryModified)).subscribe(event => {
      this.libraryService.getLibraries().pipe(take(1), shareReplay()).subscribe((libraries: Library[]) => {
        this.libraries = [...libraries];
        this.cdRef.markForCheck();
      });
    });
  }

  handleAction(action: ActionItem<Library>, library: Library) {
    switch (action.action) {
      case(Action.Scan):
        this.actionService.scanLibrary(library);
        break;
      case(Action.RefreshMetadata):
        this.actionService.refreshMetadata(library);
        break;
      case (Action.AnalyzeFiles):
        this.actionService.analyzeFiles(library);
        break;
      case (Action.Edit):
        this.actionService.editLibrary(library, () => window.scrollTo(0, 0));
        break;
      default:
        break;
    }
  }

  importCbl() {
    const ref = this.ngbModal.open(ImportCblModalComponent, {size: 'xl'});
  }

  performAction(action: ActionItem<Library>, library: Library) {
    if (typeof action.callback === 'function') {
      action.callback(action, library);
    }
  }

  getLibraryTypeIcon(format: LibraryType) {
    switch (format) {
      case LibraryType.Book:
        return 'fa-book';
      case LibraryType.Comic:
      case LibraryType.Manga:
        return 'fa-book-open';
    }
  }

  getLibraryImage(library: Library) {
    if (library.coverImage) return this.imageService.getLibraryCoverImage(library.id);
    return null;
  }

  toggleNavBar() {
    this.navService.toggleSideNav();
  }

}
