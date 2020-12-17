import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { MemberService } from '../_services/member.service';
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

    // TODO: Clean up this logic
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        // User is logged in, redirect to libraries
        this.router.navigateByUrl('/library');
      } else {
        this.memberService.getMembers().subscribe(members => {
          this.firstTimeFlow = members.filter(m => m.isAdmin).length === 0;
          console.log('First time user flow: ', this.firstTimeFlow);
        });
      }
    });
  }


  onAdminCreated(success: boolean) {
    if (success) {
      this.router.navigateByUrl('/library');
    }
  }

}
