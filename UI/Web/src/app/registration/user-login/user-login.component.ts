import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { AddEmailToAccountMigrationModalComponent } from '../_modals/add-email-to-account-migration-modal/add-email-to-account-migration-modal.component';
import { User } from '../../_models/user';
import { AccountService } from '../../_services/account.service';
import { MemberService } from '../../_services/member.service';
import { NavService } from '../../_services/nav.service';


@Component({
  selector: 'app-user-login',
  templateUrl: './user-login.component.html',
  styleUrls: ['./user-login.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserLoginComponent implements OnInit {

  //model: any = {username: '', password: ''};
  loginForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required, Validators.maxLength(32), Validators.minLength(6), Validators.pattern("^.{6,32}$")])
  });

  /**
   * If there are no admins on the server, this will enable the registration to kick in.
   */
  firstTimeFlow: boolean = true;
  /**
   * Used for first time the page loads to ensure no flashing
   */
  isLoaded: boolean = false;

  constructor(private accountService: AccountService, private router: Router, private memberService: MemberService,
    private toastr: ToastrService, private navService: NavService, private modalService: NgbModal, 
    private readonly cdRef: ChangeDetectorRef) {
      this.navService.showNavBar();
      this.navService.hideSideNav();
    }

  ngOnInit(): void {
    this.navService.showNavBar();
    this.navService.hideSideNav();
    this.cdRef.markForCheck();

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.navService.showSideNav();
        this.cdRef.markForCheck();
        this.router.navigateByUrl('/libraries');
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
  }

  onAdminCreated(user: User | null) {
    if (user != null) {
      this.firstTimeFlow = false;
      this.cdRef.markForCheck();
    } else {
      this.toastr.error('There was an issue creating the new user. Please refresh and try again.');
    }
  }

  login() {
    const model = this.loginForm.getRawValue();
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
        this.router.navigateByUrl('/libraries');
      }
    }, err => {
      if (err.error === 'You are missing an email on your account. Please wait while we migrate your account.') {
        const modalRef = this.modalService.open(AddEmailToAccountMigrationModalComponent, { scrollable: true, size: 'md' });
        modalRef.componentInstance.username = model.username;
        modalRef.closed.pipe(take(1)).subscribe(() => {
        });
      } else {
        this.toastr.error(err.error);
      }
    });
  }
}
