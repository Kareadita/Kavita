import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import {filter, map, shareReplay, take} from 'rxjs/operators';
import { ImportCblModalComponent } from 'src/app/reading-list/_modals/import-cbl-modal/import-cbl-modal.component';
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
import {switchMap} from "rxjs";
import {CommonModule} from "@angular/common";
import {SideNavItemComponent} from "../side-nav-item/side-nav-item.component";
import {FilterPipe} from "../../../pipe/filter.pipe";
import {FormsModule} from "@angular/forms";
import {TranslocoDirective} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {SentenceCasePipe} from "../../../pipe/sentence-case.pipe";
import {CustomizeDashboardModalComponent} from "../customize-dashboard-modal/customize-dashboard-modal.component";
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";

@Component({
  selector: 'app-side-nav',
  standalone: true,
  imports: [CommonModule, SideNavItemComponent, CardActionablesComponent, FilterPipe, FormsModule, TranslocoDirective, SentenceCasePipe],
  templateUrl: './side-nav.component.html',
  styleUrls: ['./side-nav.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);

  navStreams: SideNavStream[] = [];
  libraries: Library[] = [];
  actions: ActionItem<Library>[] = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
  readingListActions = [{action: Action.Import, title: 'import-cbl', children: [], requiresAdmin: true, callback: this.importCbl.bind(this)}];
  homeActions = [{action: Action.Edit, title: 'customize', children: [], requiresAdmin: false, callback: this.handleHomeActions.bind(this)}];

  filterQuery: string = '';
  filterLibrary = (library: Library) => {
    return library.name.toLowerCase().indexOf((this.filterQuery || '').toLowerCase()) >= 0;
  }
  protected readonly SideNavStreamType = SideNavStreamType;


  constructor(private libraryService: LibraryService,
    public utilityService: UtilityService, private messageHub: MessageHubService,
    private actionService: ActionService,
    public navService: NavService, private router: Router, private readonly cdRef: ChangeDetectorRef,
    private ngbModal: NgbModal, private imageService: ImageService, public readonly accountService: AccountService) {

    // this.navService.getSideNavStreams().subscribe(s => {
    //   this.navStreams = s;
    // });

      this.router.events.pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
        map(evt => evt as NavigationEnd),
        filter(() => this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet),
        switchMap(() => this.navService.sideNavCollapsed$),
        take(1),
        filter(collapsed => !collapsed)
      ).subscribe(() => {
        this.navService.toggleSideNav();
        this.cdRef.markForCheck();
      });
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (!user) return;
      this.libraryService.getLibraries().pipe(take(1), shareReplay()).subscribe((libraries: Library[]) => {
        this.libraries = libraries;
        this.cdRef.markForCheck();
      });
      this.navService.getSideNavStreams().subscribe(s => {
        this.navStreams = s;
        this.cdRef.markForCheck();
      });
    });

    // TODO: Investigate this, as it might be expensive
    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), filter(event => event.event === EVENTS.LibraryModified || event.event === EVENTS.SideNavUpdate)).subscribe(event => {
      this.libraryService.getLibraries().pipe(take(1), shareReplay()).subscribe((libraries: Library[]) => {
        this.libraries = [...libraries];
        this.cdRef.markForCheck();
      });
      this.navService.getSideNavStreams().subscribe(s => {
        this.navStreams = [...s];
        this.cdRef.markForCheck();
      });
    });
  }

  async handleAction(action: ActionItem<Library>, library: Library) {
    switch (action.action) {
      case(Action.Scan):
        await this.actionService.scanLibrary(library);
        break;
      case(Action.RefreshMetadata):
        await this.actionService.refreshMetadata(library);
        break;
      case (Action.AnalyzeFiles):
        await this.actionService.analyzeFiles(library);
        break;
      case (Action.Edit):
        this.actionService.editLibrary(library, () => window.scrollTo(0, 0));
        break;
      default:
        break;
    }
  }

  handleHomeActions() {
    this.ngbModal.open(CustomizeDashboardModalComponent, {size: 'xl'});
  }

  importCbl() {
    this.ngbModal.open(ImportCblModalComponent, {size: 'xl'});
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
