import { Component, OnInit, ViewChild } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { DirectoryPickerComponent, DirectoryPickerResult } from 'src/app/directory-picker/directory-picker.component';
import { MemberService } from 'src/app/member.service';
import { Member } from 'src/app/_models/member';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-manage-users',
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss']
})
export class ManageUsersComponent implements OnInit {

  members: Member[] = [];
  loggedInUsername = '';

  // Create User functionality
  createMemberToggle = false;

  constructor(private memberService: MemberService, public accountService: AccountService) {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user: User) => {
      this.loggedInUsername = user.username;
    });
  }

  ngOnInit(): void {
    console.log('User Component');
    this.loadMembers();
  }

  loadMembers() {
    this.memberService.getMembers().subscribe(members => {
      this.members = members;
    });
  }

  canEditMember(member: Member): boolean {
    return this.loggedInUsername !== member.username;
  }

  createMember() {
    this.createMemberToggle = true;
  }

  onMemberCreated(success: boolean) {
    this.createMemberToggle = false;
    this.loadMembers();
  }
}
