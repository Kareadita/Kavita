import {ChangeDetectorRef, Component, inject, OnInit, signal} from '@angular/core';
import {SplashContainerComponent} from "../_components/splash-container/splash-container.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {Router} from "@angular/router";
import {NavService} from "../../_services/nav.service";
import {take} from "rxjs/operators";

@Component({
  selector: 'app-oidc-callback',
  imports: [
    SplashContainerComponent,
    TranslocoDirective
  ],
  templateUrl: './oidc-callback.component.html',
  styleUrl: './oidc-callback.component.scss'
})
export class OidcCallbackComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly navService = inject(NavService);
  private readonly cdRef = inject(ChangeDetectorRef);

  showSplash = signal(false);

  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.navService.showNavBar();
        this.navService.showSideNav();
        this.router.navigateByUrl('/home');
        this.cdRef.markForCheck();
      }
    });

    // Show back to log in splash only after 1s, for a more seamless experience
    setTimeout(() => this.showSplash.set(true), 1000);
  }

  goToLogin() {
    this.router.navigateByUrl('/login');
  }
}
