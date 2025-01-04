import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {distinctUntilChanged, filter, map, take, tap} from 'rxjs/operators';
import { ImageService } from 'src/app/_services/image.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { Breakpoint, UtilityService } from '../../../shared/_services/utility.service';
import { Library, LibraryType } from '../../../_models/library/library';
import { AccountService } from '../../../_services/account.service';
import { Action, ActionFactoryService, ActionItem } from '../../../_services/action-factory.service';
import { ActionService } from '../../../_services/action.service';
import { NavService } from '../../../_services/nav.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {BehaviorSubject, merge, Observable, of, ReplaySubject, startWith, switchMap} from "rxjs";
import {AsyncPipe, NgClass} from "@angular/common";
import {SideNavItemComponent} from "../side-nav-item/side-nav-item.component";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {FormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";
import {WikiLink} from "../../../_models/wiki";
import {SettingsTabId} from "../../preference-nav/preference-nav.component";
import {LicenseService} from "../../../_services/license.service";

@Component({
  selector: 'app-side-nav',
  standalone: true,
  imports: [SideNavItemComponent, CardActionablesComponent, FilterPipe, FormsModule, TranslocoDirective, SentenceCasePipe, NgbTooltip, NgClass, AsyncPipe],
  templateUrl: './side-nav.component.html',
  styleUrls: ['./side-nav.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavComponent implements OnInit {

  private readonly router = inject(Router);
  protected readonly utilityService = inject(UtilityService);
  private readonly messageHub = inject(MessageHubService);
  private readonly actionService = inject(ActionService);
  public readonly navService = inject(NavService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly imageService = inject(ImageService);
  public readonly accountService = inject(AccountService);
  public readonly licenseService = inject(LicenseService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly actionFactoryService = inject(ActionFactoryService);

  protected readonly WikiLink = WikiLink;
  protected readonly ItemLimit = 10;
  protected readonly SideNavStreamType = SideNavStreamType;
  protected readonly SettingsTabId = SettingsTabId;

  cachedData: SideNavStream[] | null = null;
  actions: ActionItem<Library>[] = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));

  filterQuery: string = '';
  filterLibrary = (stream: SideNavStream) => {
    return stream.name.toLowerCase().indexOf((this.filterQuery || '').toLowerCase()) >= 0;
  }
  showAll: boolean = false;
  totalSize = 0;
  isReadOnly = false;

  private showAllSubject = new BehaviorSubject<boolean>(false);
  showAll$ = this.showAllSubject.asObservable();

  private loadDataSubject = new ReplaySubject<void>();
  loadData$ = this.loadDataSubject.asObservable();

  loadDataOnInit$: Observable<SideNavStream[]> = this.loadData$.pipe(
    switchMap(() => {
      if (this.cachedData != null) {
        return of(this.cachedData);
      }
      return this.navService.getSideNavStreams().pipe(
        map(data => {
          this.cachedData = data; // Cache the data after initial load
          return data;
        })
      );
    })
  );

  navStreams$ = merge(
    this.showAll$.pipe(
      startWith(false),
      distinctUntilChanged(),
      tap(showAll => this.showAll = showAll),
      switchMap(showAll =>
        showAll
          ? this.loadDataOnInit$.pipe(
            tap(d => this.totalSize = d.length),
          )
          : this.loadDataOnInit$.pipe(
            tap(d => this.totalSize = d.length),
            map(d => d.slice(0, this.ItemLimit))
          )
      ),
      takeUntilDestroyed(this.destroyRef),
    ), this.messageHub.messages$.pipe(
      filter(event => event.event === EVENTS.LibraryModified || event.event === EVENTS.SideNavUpdate),
      tap(() => {
          this.cachedData = null; // Reset cached data to null to get latest
      }),
      switchMap(() => {
        if (this.showAll) return this.loadDataOnInit$;
        else return this.loadDataOnInit$.pipe(map(d => d.slice(0, this.ItemLimit)))
      }), // Reload data when events occur
      takeUntilDestroyed(this.destroyRef),
    )
  ).pipe(
      startWith(null),
      filter(data => data !== null),
      takeUntilDestroyed(this.destroyRef),
  );

  collapseSideNavOnMobileNav$ = this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef),
      map(evt => evt as NavigationEnd),
      filter(() => this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet),
      switchMap(() => this.navService.sideNavCollapsed$),
      take(1),
      filter(collapsed => !collapsed)
  );


  constructor() {
    // Ensure that on mobile, we are collapsed by default
    if (this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet) {
      this.navService.collapseSideNav(true);
    }

    this.collapseSideNavOnMobileNav$.subscribe(() => {
        this.navService.collapseSideNav(false);
        this.cdRef.markForCheck();
    });
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (!user) return;
      this.isReadOnly = this.accountService.hasReadOnlyRole(user!);
      this.cdRef.markForCheck();
      this.loadDataSubject.next();
    });
  }

  async handleAction(action: ActionItem<Library>, library: Library) {
    switch (action.action) {
      case(Action.Scan):
        await this.actionService.scanLibrary(library);
        break;
      case(Action.RefreshMetadata):
        await this.actionService.refreshLibraryMetadata(library);
        break;
      case(Action.GenerateColorScape):
        await this.actionService.refreshLibraryMetadata(library, undefined, false);
        break;
      case (Action.AnalyzeFiles):
        await this.actionService.analyzeFiles(library);
        break;
      case (Action.Delete):
        await this.actionService.deleteLibrary(library);
        break;
      case (Action.Edit):
        this.actionService.editLibrary(library, () => window.scrollTo(0, 0));
        break;
      default:
        break;
    }
  }

  performAction(action: ActionItem<Library>, library: Library) {
    if (typeof action.callback === 'function') {
      action.callback(action, library);
    }
  }

  getLibraryTypeIcon(format: LibraryType) {
    switch (format) {
      case LibraryType.Book:
      case LibraryType.LightNovel:
        return 'fa-book';
      case LibraryType.Comic:
      case LibraryType.ComicVine:
      case LibraryType.Manga:
        return 'fa-book-open';
      case LibraryType.Images:
        return 'fa-images';
    }
  }

  getLibraryImage(library: Library) {
    if (library.coverImage) return this.imageService.getLibraryCoverImage(library.id);
    return null;
  }


  toggleNavBar() {
    this.navService.toggleSideNav();
  }

  showMore() {
    this.showAllSubject.next(true);
  }

  showLess() {
    this.filterQuery = '';
    this.cdRef.markForCheck();
    this.showAllSubject.next(false);
  }

  protected readonly Breakpoint = Breakpoint;
}
