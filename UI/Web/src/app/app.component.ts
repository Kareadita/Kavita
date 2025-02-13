import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  inject,
  OnInit
} from '@angular/core';
import {NavigationStart, Router, RouterOutlet} from '@angular/router';
import {map, shareReplay, take, tap} from 'rxjs/operators';
import {AccountService} from './_services/account.service';
import {LibraryService} from './_services/library.service';
import {NavService} from './_services/nav.service';
import {NgbModal, NgbModalConfig, NgbOffcanvas, NgbRatingConfig} from '@ng-bootstrap/ng-bootstrap';
import {AsyncPipe, DOCUMENT, NgClass} from '@angular/common';
import {filter, interval, Observable, switchMap} from 'rxjs';
import {ThemeService} from "./_services/theme.service";
import {SideNavComponent} from './sidenav/_components/side-nav/side-nav.component';
import {NavHeaderComponent} from "./nav/_components/nav-header/nav-header.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ServerService} from "./_services/server.service";
import {OutOfDateModalComponent} from "./announcements/_components/out-of-date-modal/out-of-date-modal.component";
import {PreferenceNavComponent} from "./sidenav/preference-nav/preference-nav.component";
import {Breakpoint, UtilityService} from "./shared/_services/utility.service";
import {TranslocoService} from "@jsverse/transloco";
import {User} from "./_models/user";
import {VersionService} from "./_services/version.service";
import {LicenseService} from "./_services/license.service";

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    standalone: true,
  imports: [NgClass, SideNavComponent, RouterOutlet, AsyncPipe, NavHeaderComponent, PreferenceNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent implements OnInit {

  transitionState$!: Observable<boolean>;

  private readonly destroyRef = inject(DestroyRef);
  private readonly offcanvas = inject(NgbOffcanvas);
  protected readonly navService = inject(NavService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly serverService = inject(ServerService);
  protected readonly accountService = inject(AccountService);
  private readonly libraryService = inject(LibraryService);
  private readonly ngbModal = inject(NgbModal);
  private readonly router = inject(Router);
  private readonly themeService = inject(ThemeService);
  private readonly document = inject(DOCUMENT);
  private readonly translocoService = inject(TranslocoService);
  private readonly versionService = inject(VersionService); // Needs to be injected to run background job
  private readonly licenseService = inject(LicenseService);

  protected readonly Breakpoint = Breakpoint;


  constructor(ratingConfig: NgbRatingConfig, modalConfig: NgbModalConfig) {

    modalConfig.fullscreen = 'lg';

    // Setup default rating config
    ratingConfig.max = 5;
    ratingConfig.resettable = true;

    // Close any open modals when a route change occurs
    this.router.events
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
    this.utilityService.activeBreakpointSource.next(this.utilityService.getActiveBreakpoint());
  }

  ngOnInit(): void {
    this.setDocHeight();
    this.setCurrentUser();
    this.themeService.setColorScape('');
  }


  setCurrentUser() {
    const user = this.accountService.getUserFromLocalStorage();
    this.accountService.setCurrentUser(user);

    if (!user) return;

    // Bootstrap anything that's needed
    this.themeService.getThemes().subscribe();
    this.libraryService.getLibraryNames().pipe(take(1), shareReplay({refCount: true, bufferSize: 1})).subscribe();
    if (this.accountService.hasAdminRole(user)) {
      this.licenseService.licenseInfo().subscribe();
    }
  }
}
