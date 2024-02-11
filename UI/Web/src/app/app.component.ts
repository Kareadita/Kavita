import {ChangeDetectorRef, Component, DestroyRef, HostListener, inject, Inject, OnInit} from '@angular/core';
import {NavigationStart, Router, RouterOutlet} from '@angular/router';
import {map, shareReplay, take, tap} from 'rxjs/operators';
import { AccountService } from './_services/account.service';
import { LibraryService } from './_services/library.service';
import { NavService } from './_services/nav.service';
import { filter } from 'rxjs/operators';
import {NgbModal, NgbModalConfig, NgbOffcanvas, NgbRatingConfig} from '@ng-bootstrap/ng-bootstrap';
import { DOCUMENT, NgClass, NgIf, AsyncPipe } from '@angular/common';
import {interval, Observable, switchMap} from 'rxjs';
import {ThemeService} from "./_services/theme.service";
import { SideNavComponent } from './sidenav/_components/side-nav/side-nav.component';
import {NavHeaderComponent} from "./nav/_components/nav-header/nav-header.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ServerService} from "./_services/server.service";
import {OutOfDateModalComponent} from "./announcements/_components/out-of-date-modal/out-of-date-modal.component";

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    standalone: true,
  imports: [NgClass, NgIf, SideNavComponent, RouterOutlet, AsyncPipe, NavHeaderComponent]
})
export class AppComponent implements OnInit {

  transitionState$!: Observable<boolean>;

  private readonly destroyRef = inject(DestroyRef);
  private readonly offcanvas = inject(NgbOffcanvas);
  public readonly navService = inject(NavService);
  public readonly cdRef = inject(ChangeDetectorRef);
  public readonly serverService = inject(ServerService);

  constructor(private accountService: AccountService,
    private libraryService: LibraryService,
    private router: Router, private ngbModal: NgbModal, ratingConfig: NgbRatingConfig,
    @Inject(DOCUMENT) private document: Document, private themeService: ThemeService, private modalConfig: NgbModalConfig) {

    modalConfig.fullscreen = 'md';

    // Setup default rating config
    ratingConfig.max = 5;
    ratingConfig.resettable = true;

    // Close any open modals when a route change occurs
    router.events
      .pipe(
          filter(event => event instanceof NavigationStart),
          takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(async (event) => {

        if (!this.ngbModal.hasOpenModals() && !this.offcanvas.hasOpenOffcanvas()) return;

        if (this.ngbModal.hasOpenModals()) {
          this.ngbModal.dismissAll();
        }

        if (this.offcanvas.hasOpenOffcanvas()) {
          this.offcanvas.dismiss();
        }

        if ((event as any).navigationTrigger === 'popstate') {
          const currentRoute = this.router.routerState;
          await this.router.navigateByUrl(currentRoute.snapshot.url, { skipLocationChange: true });
        }

      });


    this.transitionState$ = this.accountService.currentUser$.pipe(
      tap(user => {

      }),
      map((user) => {
      if (!user) return false;
      return user.preferences.noTransitions;
    }), takeUntilDestroyed(this.destroyRef));
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  setDocHeight() {
    // Sets a CSS variable for the actual device viewport height. Needed for mobile dev.
    const vh = window.innerHeight * 0.01;
    this.document.documentElement.style.setProperty('--vh', `${vh}px`);
  }

  ngOnInit(): void {
    this.setDocHeight();
    this.setCurrentUser();
  }

  setCurrentUser() {
    const user = this.accountService.getUserFromLocalStorage();
    this.accountService.setCurrentUser(user);

    if (user) {
      // Bootstrap anything that's needed
      this.themeService.getThemes().subscribe();
      this.libraryService.getLibraryNames().pipe(take(1), shareReplay({refCount: true, bufferSize: 1})).subscribe();
      this.libraryService.getLibraryTypes().pipe(take(1), shareReplay({refCount: true, bufferSize: 1})).subscribe();
      // On load, make an initial call for valid license
      this.accountService.hasValidLicense().subscribe();


      // Every hour, have the UI check for an update. People seriously stay out of date
      interval(2* 60 * 60 * 1000) // 2 hours in milliseconds
        .pipe(
          switchMap(() => this.accountService.currentUser$),
          filter(u => u !== undefined && this.accountService.hasAdminRole(u)),
          switchMap(_ => this.serverService.checkHowOutOfDate()),
          filter(versionOutOfDate => {
            return !isNaN(versionOutOfDate) && versionOutOfDate > 2;
          }),
          tap(versionOutOfDate => {
            if (!this.ngbModal.hasOpenModals()) {
              const ref = this.ngbModal.open(OutOfDateModalComponent, {size: 'xl', fullscreen: 'md'});
              ref.componentInstance.versionsOutOfDate = 3;
            }
          })
        )
        .subscribe();
    }
  }
}
