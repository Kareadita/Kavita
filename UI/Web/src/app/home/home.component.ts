import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { MemberService } from '../_services/member.service';
import { AccountService } from '../_services/account.service';
import { StatsService } from '../_services/stats.service';
import * as ClientUtils from "../shared/utils/clientUtils";

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

  constructor(public accountService: AccountService, private memberService: MemberService, private router: Router, private statsService: StatsService) {
  }

  ngOnInit(): void {

    this.memberService.adminExists().subscribe(adminExists => {
      this.firstTimeFlow = !adminExists;

      if (this.firstTimeFlow) {
        return;
      }

      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
    
        this.statsService.sendClientInfo(ClientUtils.getClientInfo())
        .pipe(take(1))
        .subscribe(resp => {/* No Operation */});

        if (user) {
          // Check if user came here from another url, else send to library route
          const pageResume = localStorage.getItem('kavita--auth-intersection-url');
          if (pageResume && pageResume !== '/no-connection') {
            localStorage.setItem('kavita--auth-intersection-url', '');
            this.router.navigateByUrl(pageResume);
          } else {
            this.router.navigateByUrl('/library');
          }
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
