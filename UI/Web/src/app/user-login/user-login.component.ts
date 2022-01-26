import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { first, take } from 'rxjs/operators';
import { SettingsService } from '../admin/settings.service';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { MemberService } from '../_services/member.service';
import { NavService } from '../_services/nav.service';

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
  authDisabled: boolean = false;
  /**
   * If there are no admins on the server, this will enable the registration to kick in.
   */
  firstTimeFlow: boolean = true;
  /**
   * Used for first time the page loads to ensure no flashing
   */
  isLoaded: boolean = false;

  constructor(private accountService: AccountService, private router: Router, private memberService: MemberService, 
    private toastr: ToastrService, private navService: NavService, private settingsService: SettingsService) { }

  ngOnInit(): void {
    this.navService.showNavBar();
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.router.navigateByUrl('/library');
      }
    });

    this.settingsService.getAuthenticationEnabled().pipe(take(1)).subscribe((enabled: boolean) => {
      // There is a bug where this is coming back as a string not a boolean.
      this.authDisabled = !enabled;
      if (this.authDisabled) {
        this.loginForm.get('password')?.setValidators([]);

        // This API is only useable on disabled authentication
        this.memberService.getMemberNames().pipe(take(1)).subscribe(members => {
          this.memberNames = members;
          const isOnlyOne = this.memberNames.length === 1;
          this.memberNames.forEach(name => this.isCollapsed[name] = !isOnlyOne);
          this.firstTimeFlow = members.length === 0;
          this.isLoaded = true;
        });
      } else {
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
      if (this.authDisabled) {
        this.isCollapsed[user.username] = true;
        this.select(user.username);
        this.memberNames.push(user.username);
      }
    } else {
      this.toastr.error('There was an issue creating the new user. Please refresh and try again.');
    }
  }

  login() {
    this.model = this.loginForm.getRawValue(); // {username: this.loginForm.get('username')?.value, password: this.loginForm.get('password')?.value};
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
      if (err.error === 'You must confirm your email first') {
      }
      this.toastr.error(err.error);
    });

    // TODO: Move this into account service so it always happens
    this.accountService.currentUser$
      .pipe(first(x => (x !== null && x !== undefined && typeof x !== 'undefined')))
      .subscribe(currentUser => {
        this.navService.setDarkMode(currentUser.preferences.siteDarkMode);
      });
  }

  select(member: string) {
    // This is a special case
    if (member === ' Login ' && !this.authDisabled) {
      return;
    }

    this.loginForm.get('username')?.setValue(member);

    this.isCollapsed[member] = !this.isCollapsed[member];
    this.collapseAllButName(member);
    // ?! Scroll to the newly opened element? 
  }

  collapseAllButName(name: string) {
    Object.keys(this.isCollapsed).forEach(key => {
      if (key !== name) {
        this.isCollapsed[key] = true;
      }
    });
  }
}
