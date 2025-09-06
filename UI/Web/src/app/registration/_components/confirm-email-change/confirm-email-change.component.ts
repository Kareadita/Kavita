import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { ThemeService } from 'src/app/_services/theme.service';
import { SplashContainerComponent } from '../splash-container/splash-container.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";

/**
 * This component just validates the email via API then redirects to login
 */
@Component({
    selector: 'app-confirm-email-change',
    templateUrl: './confirm-email-change.component.html',
    styleUrls: ['./confirm-email-change.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [SplashContainerComponent, TranslocoDirective]
})
export class ConfirmEmailChangeComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private toastr = inject(ToastrService);
  private themeService = inject(ThemeService);
  private navService = inject(NavService);
  private readonly cdRef = inject(ChangeDetectorRef);


  email: string = '';
  token: string = '';

  confirmed: boolean = false;

  constructor() {
      this.navService.hideSideNav();
      this.themeService.setTheme(this.themeService.defaultTheme);
      const token = this.route.snapshot.queryParamMap.get('token');
      const email = this.route.snapshot.queryParamMap.get('email');

      if (this.isNullOrEmpty(token) || this.isNullOrEmpty(email)) {
        // This is not a valid url, redirect to login
        this.toastr.error(translate('errors.invalid-confirmation-url'));
        this.router.navigateByUrl('login');
        return;
      }

      this.token = token!;
      this.email = email!;
  }

  ngOnInit(): void {
    this.accountService.confirmEmailUpdate({email: this.email, token: this.token}).subscribe((errors) => {
      this.confirmed = true;
      this.cdRef.markForCheck();

      // Once we are confirmed, we need to refresh our user information (in case the user is already authenticated)
      this.accountService.refreshAccount().subscribe();
      setTimeout(() => this.router.navigateByUrl('login'), 2000);
    });
  }

  isNullOrEmpty(v: string | null | undefined) {
    return v == undefined || v === '' || v === null;
  }

}
