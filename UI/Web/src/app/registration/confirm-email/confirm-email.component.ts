import { Component, Input, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-confirm-email',
  templateUrl: './confirm-email.component.html',
  styleUrls: ['./confirm-email.component.scss']
})
export class ConfirmEmailComponent implements OnInit {


  /**
   * Email token used for validating
   */
  token: string = '';

  registerForm: FormGroup = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    username: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required, Validators.maxLength(32), Validators.minLength(6)]),
  });

  /**
   * Validation errors from API
   */
  errors: Array<string> = [];


  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService, private toastr: ToastrService) {

    const token = this.route.snapshot.queryParamMap.get('token');
    const email = this.route.snapshot.queryParamMap.get('email');
    if (token == undefined || token === '' || token === null) {
      // This is not a valid url, redirect to login
      this.toastr.error('Invalid confirmation email');
      this.router.navigateByUrl('login');
      return;
    }
    this.token = token;
    this.registerForm.get('email')?.setValue(email || '');
  }

  ngOnInit(): void {
  }

  submit() {
    let model = this.registerForm.getRawValue();
    model.token = this.token;
    this.accountService.confirmEmail(model).subscribe((user) => {
      this.toastr.success('Account registration complete');
      this.router.navigateByUrl('login');
    }, err => {
      this.errors = err;
    });
  }

}
