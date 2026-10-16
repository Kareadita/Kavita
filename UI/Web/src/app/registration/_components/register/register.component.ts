import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {Router} from '@angular/router';
import {ToastrService} from '@openng/ngx-toastr';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgTemplateOutlet} from '@angular/common';
import {SplashContainerComponent} from '../splash-container/splash-container.component';
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {NavService} from "../../../_services/nav.service";
import {AccountService} from "../../../_services/account.service";
import {MemberService} from "../../../_services/member.service";
import {LocalizationService} from "../../../_services/localization.service";
import {KavitaLocale} from "../../../_models/metadata/language";
import {
  displayLocaleName,
  safeSet,
  KavitaLocaleKey,
  KavitaLocaleSourceKey,
  LocaleSourceExplicit
} from "../../../../libs/locale-utils";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, maxLength, minLength, pattern, required} from "@angular/forms/signals";

/**
 * This is exclusively used to register the first user on the server and nothing else
 */
@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
  imports: [SplashContainerComponent, NgbTooltip, NgTemplateOutlet, TranslocoDirective, FormFieldDirective, ValidationErrorsComponent, FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent implements OnInit {

  private readonly navService = inject(NavService);
  private readonly router = inject(Router);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly memberService = inject(MemberService);
  private readonly localizationService = inject(LocalizationService);
  private readonly translocoService = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Register page picker is local-only (registration lands on the login page,
   * first-login sync persists it). It never writes to the database directly.
   */
  locales = signal<KavitaLocale[]>([]);
  selectedLocale = signal<string>('en');

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

  ngOnInit(): void {
    this.localizationService.getLocales().pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: locales => {
        this.locales.set([...locales].sort((a, b) => a.renderName.localeCompare(b.renderName, undefined, {sensitivity: 'base'})));
        this.selectedLocale.set(this.translocoService.getActiveLang());
      },
      error: () => this.selectedLocale.set(this.translocoService.getActiveLang())
    });
  }

  formModel = signal({
    username: '',
    email: '',
    password: ''
  });
  formGroup = form(this.formModel, path => {
    required(path.username);
    required(path.password);
    minLength(path.password, 6);
    maxLength(path.password, 256);
    pattern(path.password, /^.{6,256}$/);
  });

  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();

      this.memberService.adminExists().subscribe(adminExists => {
      if (adminExists) {
        this.router.navigateByUrl('login');
        return;
      }
    });
  }

  submit() {
    if (!this.formGroup().valid()) return;

    this.accountService.register(this.formModel()).subscribe((user) => {
      this.toastr.success(translate('toasts.account-registration-complete'));
      this.router.navigateByUrl('login');
    });
  }
}
