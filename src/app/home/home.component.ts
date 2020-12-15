import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { take } from 'rxjs/operators';
import { MemberService } from '../member.service';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {

  firstTimeFlow = false;
  model: any = {};
  registerForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required])
  });

  constructor(public accountService: AccountService, private memberService: MemberService, private router: Router) {
  }

  ngOnInit(): void {

    this.memberService.getMembers().subscribe(members => {
      this.firstTimeFlow = members.filter(m => m.isAdmin).length === 0;
      console.log('First time user flow: ', this.firstTimeFlow);
    });

  }

  register() {
    this.model.isAdmin = this.firstTimeFlow;

    console.log('Registering: ', this.model);
    this.accountService.register(this.model).subscribe(resp => {
      this.router.navigateByUrl('/library');
    }, err => {
      console.log('validation errors from interceptor', err);
    });
  }

}
