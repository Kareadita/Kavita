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
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { AccountService } from '../../_services/account.service';
import { MemberService } from '../../_services/member.service';
import { NavService } from '../../_services/nav.service';
import { SplashContainerComponent } from '../_components/splash-container/splash-container.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {environment} from "../../../environments/environment";
import {ImageComponent} from "../../shared/image/image.component";
import { SettingsService } from 'src/app/admin/settings.service';
import {OidcPublicConfig} from "../../admin/_models/oidc-config";


@Component({
    selector: 'app-user-login',
    templateUrl: './user-login.component.html',
    styleUrls: ['./user-login.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SplashContainerComponent, ReactiveFormsModule, RouterLink, TranslocoDirective, ImageComponent]
})
export class UserLoginComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly memberService = inject(MemberService);
  private readonly toastr = inject(ToastrService);
  private readonly navService = inject(NavService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  protected readonly settingsService = inject(SettingsService);

  baseUrl = environment.apiUrl.substring(0, environment.apiUrl.indexOf("api"));

  loginForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required, Validators.maxLength(256), Validators.minLength(6), Validators.pattern("^.{6,256}$")])
  });

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
   * Set from query
   */
  forceShowPasswordLogin = signal(false);
  oidcConfig = signal<OidcPublicConfig | undefined>(undefined);

  /**
   * Display the login form
   */
  showPasswordLogin = computed(() => {
    const loaded = this.isLoaded();
    const config = this.oidcConfig();
    const force = this.forceShowPasswordLogin();
    if (force) return true;

    return loaded && config && !(config.enabled && config.disablePasswordAuthentication);
  });
  showOidcButton = computed(() => this.oidcConfig()?.enabled ?? false);

  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();

    effect(() => {
      const skipAutoLogin = this.skipAutoLogin();
      const oidcConfig = this.oidcConfig();

      if (!oidcConfig || !oidcConfig.enabled || skipAutoLogin === undefined) return;

      if (oidcConfig.autoLogin && !skipAutoLogin) {
        window.location.href = this.baseUrl + 'oidc/login';
      }
    });
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.navService.handleLogin()
        this.cdRef.markForCheck();
      }
    });

    this.settingsService.getPublicOidcConfig().subscribe(config => {
      this.oidcConfig.set(config);
    });

    this.memberService.adminExists().pipe(take(1)).subscribe(adminExists => {
      if (!adminExists) {
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
      this.forceShowPasswordLogin.set(params.get('forceShowPassword') === 'true');

      const error = params.get('error');
      if (!error) return;

      if (error.startsWith('errors.')) {
        this.toastr.error(translate(error));
      } else {
        this.toastr.error(error);
      }
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
