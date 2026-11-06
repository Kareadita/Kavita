import {ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, OnInit, signal} from '@angular/core';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {ToastrService} from '@openng/ngx-toastr';
import {AccountService} from '../../_services/account.service';
import {MemberService} from '../../_services/member.service';
import {NavService} from '../../_services/nav.service';
import {SplashContainerComponent} from '../_components/splash-container/splash-container.component';
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {LocalizationService} from "../../_services/localization.service";
import {KavitaLocale} from "../../_models/metadata/language";
import {
  displayLocaleName,
  safeSet,
  KavitaLocaleKey,
  KavitaLocaleSourceKey,
  LocaleSourceExplicit
} from "../../../libs/locale-utils";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {environment} from "../../../environments/environment";
import {ImageComponent} from "../../shared/image/image.component";
import {OidcPublicConfig} from "../../admin/_models/oidc-config";
import {SettingsService} from "../../admin/settings.service";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {form, FormField, FormRoot, required} from "@angular/forms/signals";

interface LoginFormModel {
  username: string;
  password: string;
}

@Component({
  selector: 'app-user-login',
  templateUrl: './user-login.component.html',
  styleUrls: ['./user-login.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SplashContainerComponent, RouterLink, TranslocoDirective, ImageComponent,
    ValidationErrorsComponent, FormFieldDirective, FormField, FormRoot]
})
export class UserLoginComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly memberService = inject(MemberService);
  private readonly toastr = inject(ToastrService);
  private readonly navService = inject(NavService);
  private readonly route = inject(ActivatedRoute);
  protected readonly settingsService = inject(SettingsService);
  private readonly localizationService = inject(LocalizationService);
  private readonly translocoService = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  baseUrl = environment.apiUrl.substring(0, environment.apiUrl.indexOf('api'));

  formModel = signal<LoginFormModel>({
    username: '',
    password: '',
  });
  formGroup = form(this.formModel, (path) => {
    required(path.username);
    required(path.password);
    // For login screen, we hide validation
    // maxLength(path.password, 256);
    // minLength(path.password, 6);
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
  oidcConfig = signal<OidcPublicConfig | undefined>(undefined);

  /**
   * Display the login form
   */
  showPasswordLogin = computed(() => {
    const loaded = this.isLoaded();
    const config = this.oidcConfig();

    return loaded && config && !(config.enabled && config.disablePasswordAuthentication);
  });
  showOidcButton = computed(() => this.oidcConfig()?.enabled ?? false);

  /**
   * Pre-login language picker. The locale endpoint allows anonymous access,
   * so this works logged out. It only writes local storage; persisting to the
   * account happens once in the login sync below.
   */
  locales = signal<KavitaLocale[]>([]);
  selectedLocale = signal<string>('en');
  // Plain field (not a signal) so the effect below runs exactly once per login
  private localeSynced = false;

  /** Chinese entries render native names (backend RenderName is always English) */
  localeDisplayName = displayLocaleName;

  changeLocale(event: Event) {
    const lang = (event.target as HTMLSelectElement).value;
    // Whitelist against server locales so a bad value can never reach the loader URL
    if (!lang || !this.locales().some(l => l.fileName === lang)) return;
    const previous = this.selectedLocale();
    this.selectedLocale.set(lang);
    safeSet(KavitaLocaleKey, lang);
    safeSet(KavitaLocaleSourceKey, LocaleSourceExplicit);
    this.localizationService.refreshTranslations(lang).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      error: () => this.selectedLocale.set(previous)
    });
  }

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

    effect(() => {
      const user = this.accountService.currentUser();
      if (!user) {
        this.localeSynced = false;
        return;
      }
      if (this.localeSynced) {
        this.navService.handleLogin();
        return;
      }
      this.localeSynced = true;
      // Serialize locale sync before navigating home to avoid language flashing.
      // The sync observable always completes (one-shot HTTP or empty), so no teardown needed.
      this.accountService.syncLocaleAfterLogin().subscribe({
        next: () => this.navService.handleLogin(),
        error: () => this.navService.handleLogin()
      });
    });
  }

  ngOnInit(): void {
    this.localizationService.getLocales().pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: locales => {
        this.locales.set([...locales].sort((a, b) => a.renderName.localeCompare(b.renderName, undefined, {sensitivity: 'base'})));
        this.selectedLocale.set(this.translocoService.getActiveLang());
      },
      // A failed locale list must never block login; fall back to the active language
      error: () => this.selectedLocale.set(this.translocoService.getActiveLang())
    });

    this.settingsService.getPublicOidcConfig().subscribe(config => {
      this.oidcConfig.set(config);
    });

    this.memberService.adminExists().subscribe(adminExists => {
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
    const model = {
      ...this.formModel(),
      apiKey
    };

    this.isSubmitting.set(true);
    this.accountService.login(model).subscribe({
       next: () => {
           this.formGroup().reset();
           // 跳转由 currentUser effect 统一处理（先完成语言同步再进首页）
           this.isSubmitting.set(false);
       },
      error: (err) => {
        this.toastr.error(err.error);
        this.isSubmitting.set(false);
      }
    });
  }
}
