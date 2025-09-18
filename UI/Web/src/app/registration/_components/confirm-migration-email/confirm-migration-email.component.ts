import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ThemeService } from 'src/app/_services/theme.service';
import { AccountService } from 'src/app/_services/account.service';
import {translate} from "@jsverse/transloco";

@Component({
    selector: 'app-confirm-migration-email',
    templateUrl: './confirm-migration-email.component.html',
    styleUrls: ['./confirm-migration-email.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true
})
export class ConfirmMigrationEmailComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private toastr = inject(ToastrService);
  private themeService = inject(ThemeService);


  constructor() {

    this.themeService.setTheme(this.themeService.defaultTheme);
    const token = this.route.snapshot.queryParamMap.get('token');
    const email = this.route.snapshot.queryParamMap.get('email');

    if (token === undefined || token === '' || token === null || email === undefined || email === '' || email === null) {
      // This is not a valid url, redirect to login
      this.toastr.error(translate('errors.invalid-confirmation-email'));
      this.router.navigateByUrl('login');
      return;
    }
    this.accountService.confirmMigrationEmail({token: token, email}).subscribe((user) => {
      this.toastr.success(translate('toasts.account-migration-complete'));
      this.router.navigateByUrl('login');
    });

  }
}
