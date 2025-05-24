import {ChangeDetectorRef, Component, OnInit} from '@angular/core';
import {SplashContainerComponent} from "../_components/splash-container/splash-container.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {Router} from "@angular/router";
import {NavService} from "../../_services/nav.service";
import {take} from "rxjs/operators";
import {OidcService} from "../../_services/oidc.service";

@Component({
  selector: 'app-oidc-callback',
  imports: [
    SplashContainerComponent,
    TranslocoDirective
  ],
  templateUrl: './oidc-callback.component.html',
  styleUrl: './oidc-callback.component.scss'
})
export class OidcCallbackComponent implements OnInit{

  error: string = '';

  constructor(
    private accountService: AccountService,
    private router: Router,
    private navService: NavService,
    private readonly cdRef: ChangeDetectorRef,
    private oidcService: OidcService,
  ) {
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
  }

  goToLogin() {
    this.router.navigateByUrl('/login');
  }
}
