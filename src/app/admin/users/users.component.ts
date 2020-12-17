import { Component, OnInit } from '@angular/core';
import { MemberService } from 'src/app/member.service';
import { Member } from 'src/app/_models/member';

@Component({
  selector: 'app-users',
  templateUrl: './users.component.html',
  styleUrls: ['./users.component.scss']
})
export class UsersComponent implements OnInit {

  members: Member[] = [];

  constructor(private memberService: MemberService) { }

  ngOnInit(): void {
    console.log('User Component');
    this.memberService.getMembers().subscribe(members => {
      this.members = members;
    });
  }

}
