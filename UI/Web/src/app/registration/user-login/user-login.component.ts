import {AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit} from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { AccountService } from '../../_services/account.service';
import { MemberService } from '../../_services/member.service';
import { NavService } from '../../_services/nav.service';
import { NgIf } from '@angular/common';
import { SplashContainerComponent } from '../_components/splash-container/splash-container.component';
import {TRANSLOCO_SCOPE, TranslocoDirective} from "@jsverse/transloco";


@Component({
    selector: 'app-user-login',
    templateUrl: './user-login.component.html',
    styleUrls: ['./user-login.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SplashContainerComponent, NgIf, ReactiveFormsModule, RouterLink, TranslocoDirective]
})
export class UserLoginComponent implements OnInit {

  loginForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required, Validators.maxLength(256), Validators.minLength(6), Validators.pattern("^.{6,256}$")])
  });

  /**
   * If there are no admins on the server, this will enable the registration to kick in.
   */
  firstTimeFlow: boolean = true;
  /**
   * Used for first time the page loads to ensure no flashing
   */
  isLoaded: boolean = false;
  isSubmitting = false;

  constructor(private accountService: AccountService, private router: Router, private memberService: MemberService,
    private toastr: ToastrService, private navService: NavService,
    private readonly cdRef: ChangeDetectorRef, private route: ActivatedRoute) {
      this.navService.hideNavBar();
      this.navService.hideSideNav();
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
      this.firstTimeFlow = !adminExists;

      if (this.firstTimeFlow) {
        this.router.navigateByUrl('registration/register');
        return;
      }

      this.isLoaded = true;
      this.cdRef.markForCheck();
    });

    this.route.queryParamMap.subscribe(params => {
      const val = params.get('apiKey');
      if (val != null && val.length > 0) {
        this.login(val);
      }
    });
  }



  login(apiKey: string = '') {
    const model = this.loginForm.getRawValue();
    model.apiKey = apiKey;
    this.isSubmitting = true;
    this.cdRef.markForCheck();
    this.accountService.login(model).subscribe(() => {
      this.loginForm.reset();
      this.navService.showNavBar();
      this.navService.showSideNav();

      // Check if user came here from another url, else send to library route
      const pageResume = localStorage.getItem('kavita--auth-intersection-url');
      if (pageResume && pageResume !== '/login') {
        localStorage.setItem('kavita--auth-intersection-url', '');
        this.router.navigateByUrl(pageResume);
      } else {
        localStorage.setItem('kavita--auth-intersection-url', '');
        this.router.navigateByUrl('/home');
      }
      this.isSubmitting = false;
      this.cdRef.markForCheck();
    }, err => {
      this.toastr.error(err.error);
      this.isSubmitting = false;
      this.cdRef.markForCheck();
    });
  }
}
