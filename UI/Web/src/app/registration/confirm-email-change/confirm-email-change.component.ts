import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { ThemeService } from 'src/app/_services/theme.service';

/**
 * This component just validates the email via API then redirects to login
 */
@Component({
  selector: 'app-confirm-email-change',
  templateUrl: './confirm-email-change.component.html',
  styleUrls: ['./confirm-email-change.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmEmailChangeComponent implements OnInit {

  email: string = '';
  token: string = '';

  confirmed: boolean = false;

  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService, 
    private toastr: ToastrService, private themeService: ThemeService, private navService: NavService, 
    private readonly cdRef: ChangeDetectorRef) {
      this.navService.hideSideNav();
      this.themeService.setTheme(this.themeService.defaultTheme);
      const token = this.route.snapshot.queryParamMap.get('token');
      const email = this.route.snapshot.queryParamMap.get('email');

      if (token == undefined || token === '' || token === null) {
        // This is not a valid url, redirect to login
        this.toastr.error('Invalid confirmation url');
        //this.router.navigateByUrl('login');
        return;
      }
      if (email == undefined || email === '' || email === null) {
        // This is not a valid url, redirect to login
        this.toastr.error('Invalid confirmation email');
        //this.router.navigateByUrl('login');
        return;
      }
      this.token = token;
      this.email = email;
  }

  ngOnInit(): void {
    this.accountService.confirmEmailUpdate({email: this.email, token: this.token}).subscribe((errors) => {
      this.confirmed = true;
      this.cdRef.markForCheck();
      setTimeout(() => this.router.navigateByUrl('login'), 2000);
    });
  }

}
