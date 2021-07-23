import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { MemberService } from '../_services/member.service';
import { AccountService } from '../_services/account.service';
import { Title } from '@angular/platform-browser';

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

  constructor(public accountService: AccountService, private memberService: MemberService, private router: Router, private titleService: Title) {
  }

  ngOnInit(): void {

    this.memberService.adminExists().subscribe(adminExists => {
      this.firstTimeFlow = !adminExists;

      if (this.firstTimeFlow) {
        return;
      }

      this.titleService.setTitle('Kavita');
      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
        if (user) {
          this.router.navigateByUrl('/library');
        } else {
          this.router.navigateByUrl('/login');
        }
      });
    });
  }


  onAdminCreated(success: boolean) {
    if (success) {
      this.router.navigateByUrl('/login');
    }
  }
}
