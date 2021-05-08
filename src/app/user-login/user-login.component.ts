import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AccountService } from '../_services/account.service';
import { MemberService } from '../_services/member.service';

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

  constructor(private accountService: AccountService, private router: Router, private memberService: MemberService, private toastr: ToastrService) { }

  ngOnInit(): void {
    // Validate that there are users so you can refresh to home. This is important for first installs
    this.validateAdmin();
  }

  validateAdmin() {
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
      this.router.navigateByUrl('/library');
    }, err => {
      this.toastr.error(err.error);
    });
  }

}
