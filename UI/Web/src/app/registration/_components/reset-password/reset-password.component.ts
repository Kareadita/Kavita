import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import { Validators, FormGroup, FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from 'src/app/_services/account.service';
import { NgIf } from '@angular/common';
import { SplashContainerComponent } from '../splash-container/splash-container.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {NavService} from "../../../_services/nav.service";

@Component({
    selector: 'app-reset-password',
    templateUrl: './reset-password.component.html',
    styleUrls: ['./reset-password.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [SplashContainerComponent, ReactiveFormsModule, NgIf, TranslocoDirective]
})
export class ResetPasswordComponent {

  private readonly router = inject(Router);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly navService = inject(NavService);

  registerForm: FormGroup = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
  });

  constructor() {
    this.navService.hideNavBar();
    this.navService.hideSideNav();
  }

  submit() {
    const model = this.registerForm.get('email')?.value;
    this.accountService.requestResetPasswordEmail(model).subscribe((resp: string) => {
      this.toastr.info(resp);
      this.router.navigateByUrl('login');
    }, err => {
      this.toastr.error(err.error);
    });
  }

}
