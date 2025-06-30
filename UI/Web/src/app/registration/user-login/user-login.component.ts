import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, computed,
  effect, inject,
  OnInit,
  signal
} from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { AccountService } from '../../_services/account.service';
import { MemberService } from '../../_services/member.service';
import { NavService } from '../../_services/nav.service';
import {NgOptimizedImage} from '@angular/common';
import { SplashContainerComponent } from '../_components/splash-container/splash-container.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {environment} from "../../../environments/environment";
import {OidcService} from "../../_services/oidc.service";


@Component({
    selector: 'app-user-login',
    templateUrl: './user-login.component.html',
    styleUrls: ['./user-login.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SplashContainerComponent, ReactiveFormsModule, RouterLink, TranslocoDirective, NgbTooltip, NgOptimizedImage]
})
export class UserLoginComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly memberService = inject(MemberService);
  private readonly toastr = inject(ToastrService);
  private readonly navService = inject(NavService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  protected readonly oidcService = inject(OidcService);

  baseUrl = environment.apiUrl;

  loginForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required, Validators.maxLength(256), Validators.minLength(6), Validators.pattern("^.{6,256}$")])
  });

  /**
   * If there are no admins on the server, this will enable the registration to kick in.
   */
  firstTimeFlow = signal(true);
  /**
   * Used for first time the page loads to ensure no flashing
   */
  isLoaded = signal(false);
  isSubmitting = signal(false);
  /**
   * undefined until query params are read
   */
  skipAutoLogin = signal<boolean | undefined>(undefined);

  /**
   * Display the login form, regardless if the password authentication is disabled (admins can still log in)
   */
  forceShowPasswordLogin = signal(false);
  /**
   * Display the login form
   */
  showPasswordLogin = computed(() => {
    const loaded = this.isLoaded();
    const config = this.oidcService.settings();
    const force = this.forceShowPasswordLogin();
    if (force) return true;

    return loaded && config && !config.disablePasswordAuthentication;
  });

  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();

    effect(() => {
      const skipAutoLogin = this.skipAutoLogin();
      const oidcConfig = this.oidcService.settings();
      if (!oidcConfig || skipAutoLogin === undefined) return;

      if (oidcConfig.autoLogin && !skipAutoLogin) {
        this.oidcService.login()
      }
    });
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


    this.memberService.adminExists().pipe(take(1)).subscribe(adminExists => {
      this.firstTimeFlow.set(!adminExists);

      if (this.firstTimeFlow()) {
        this.router.navigateByUrl('registration/register');
        return;
      }

      this.isLoaded.set(true);
    });

    this.route.queryParamMap.subscribe(params => {
      const val = params.get('apiKey');
      if (val != null && val.length > 0) {
        this.login(val);
        return;
      }

      this.skipAutoLogin.set(params.get('skipAutoLogin') === 'true')
    });
  }



  login(apiKey: string = '') {
    const model = this.loginForm.getRawValue();
    model.apiKey = apiKey;
    this.isSubmitting.set(true);
    this.accountService.login(model).subscribe({
      next: () => {
          this.loginForm.reset();
          this.navService.handleLogin()

          this.isSubmitting.set(false);
      },
      error: (err) => {
        this.toastr.error(err.error);
        this.isSubmitting.set(false);
      }
    });
  }
}
