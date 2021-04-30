import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MemberService } from '../_services/member.service';

@Component({
  selector: 'app-not-connected',
  templateUrl: './not-connected.component.html',
  styleUrls: ['./not-connected.component.scss']
})
export class NotConnectedComponent implements OnInit {
  constructor(private memberService: MemberService, private router: Router) { }

  ngOnInit(): void {
    // We make a call to backend on refresh so that if it's up, we can redirect to /home
    this.memberService.adminExists().subscribe((exists) => {
      const pageResume = localStorage.getItem('kavita--no-connection-url');
      if (pageResume && pageResume !== '/no-connection') {
        localStorage.setItem('kavita--no-connection-url', '');
        this.router.navigateByUrl(pageResume);
      } else {
        this.router.navigateByUrl('/home');
      }
    });
  }

}
