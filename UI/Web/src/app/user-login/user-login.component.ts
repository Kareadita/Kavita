import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { SettingsService } from '../admin/settings.service';
import { AddEmailToAccountMigrationModalComponent } from '../registration/add-email-to-account-migration-modal/add-email-to-account-migration-modal.component';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { MemberService } from '../_services/member.service';
import { NavService } from '../_services/nav.service';

// TODO: Move this into registration module
@Component({
  selector: 'app-user-login',
  templateUrl: './user-login.component.html',
  styleUrls: ['./user-login.component.scss']
})
export class UserLoginComponent implements OnInit {

  model: any = {username: '', password: ''};
  loginForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required])
  });

  memberNames: Array<string> = [];
  isCollapsed: {[key: string]: boolean} = {};
  /**
   * If there are no admins on the server, this will enable the registration to kick in.
   */
  firstTimeFlow: boolean = true;
  /**
   * Used for first time the page loads to ensure no flashing
   */
  isLoaded: boolean = false;

  constructor(private accountService: AccountService, private router: Router, private memberService: MemberService,
    private toastr: ToastrService, private navService: NavService, private settingsService: SettingsService, private modalService: NgbModal) { }

  ngOnInit(): void {
    this.navService.showNavBar();
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.router.navigateByUrl('/library');
      }
    });

    this.memberService.adminExists().pipe(take(1)).subscribe(adminExists => {
      this.firstTimeFlow = !adminExists;

      if (this.firstTimeFlow) {
        this.router.navigateByUrl('registration/register');
        return;
      }

      this.setupAuthenticatedLoginFlow();
      this.isLoaded = true;
    });
  }

  setupAuthenticatedLoginFlow() {
    if (this.memberNames.indexOf(' Login ') >= 0) { return; }
    this.memberNames.push(' Login ');
      this.memberNames.forEach(name => this.isCollapsed[name] = false);
      const lastLogin = localStorage.getItem(this.accountService.lastLoginKey);
      if (lastLogin != undefined && lastLogin != null && lastLogin != '') {
        this.loginForm.get('username')?.setValue(lastLogin);
      }
  }

  onAdminCreated(user: User | null) {
    if (user != null) {
      this.firstTimeFlow = false;
    } else {
      this.toastr.error('There was an issue creating the new user. Please refresh and try again.');
    }
  }

  login() {
    this.model = this.loginForm.getRawValue();
    this.accountService.login(this.model).subscribe(() => {
      this.loginForm.reset();
      this.navService.showNavBar();

      // Check if user came here from another url, else send to library route
      const pageResume = localStorage.getItem('kavita--auth-intersection-url');
      if (pageResume && pageResume !== '/no-connection' && pageResume !== '/login') {
        localStorage.setItem('kavita--auth-intersection-url', '');
        this.router.navigateByUrl(pageResume);
      } else {
        this.router.navigateByUrl('/library');
      }
    }, err => {
      if (err.error === 'You are missing an email on your account. Please wait while we migrate your account.') {
        const modalRef = this.modalService.open(AddEmailToAccountMigrationModalComponent, { scrollable: true, size: 'md' });
        modalRef.componentInstance.username = this.model.username;
        modalRef.closed.pipe(take(1)).subscribe(() => {

        });
      } else {
        this.toastr.error(err.error);
      }
    });
  }
}
