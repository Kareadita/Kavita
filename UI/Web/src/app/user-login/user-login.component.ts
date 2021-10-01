import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { first, take } from 'rxjs/operators';
import { SettingsService } from '../admin/settings.service';
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

  constructor(private accountService: AccountService, private router: Router, private memberService: MemberService, 
    private toastr: ToastrService, private navService: NavService, private settingsService: SettingsService) { }

  ngOnInit(): void {

    this.settingsService.getAuthenticationEnabled().pipe(take(1)).subscribe((enabled: boolean) => {
      console.log('authed: ', enabled);
      const isAuthenticated: boolean = enabled;
      // There is a bug where this is coming back as a string not a boolean.
      if (isAuthenticated + '' === 'false') {
        this.loginForm.get('password')?.setValidators([]);
      }
    })
    


    // Validate that there are users so you can refresh to home. This is important for first installs
    this.validateAdmin();
  }

  validateAdmin() {
    this.navService.hideNavBar();
    this.memberService.adminExists().subscribe(res => {
      if (!res) {
        this.router.navigateByUrl('/home');
      }
    });
  }

  login() {
    if (!this.loginForm.dirty || !this.loginForm.valid) { return; }
    this.model = {username: this.loginForm.get('username')?.value, password: this.loginForm.get('password')?.value};
    this.accountService.login(this.model).subscribe(() => {
      this.loginForm.reset();
      this.navService.showNavBar();

      // Check if user came here from another url, else send to library route
      const pageResume = localStorage.getItem('kavita--auth-intersection-url');
      if (pageResume && pageResume !== '/no-connection') {
        localStorage.setItem('kavita--auth-intersection-url', '');
        this.router.navigateByUrl(pageResume);
      } else {
        this.router.navigateByUrl('/library');
      }
    }, err => {
      this.toastr.error(err.error);
    });

    this.accountService.currentUser$
      .pipe(first(x => (x !== null && x !== undefined && typeof x !== 'undefined')))
      .subscribe(currentUser => {
        this.navService.setDarkMode(currentUser.preferences.siteDarkMode);
      });
  }

}
