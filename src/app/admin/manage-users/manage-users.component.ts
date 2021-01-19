import { Component, OnInit, ViewChild } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { MemberService } from 'src/app/_services/member.service';
import { Member } from 'src/app/_models/member';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { LibraryAccessModalComponent } from '../_modals/library-access-modal/library-access-modal.component';
import { ToastrService } from 'ngx-toastr';

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
  loadingMembers = false;

  constructor(private memberService: MemberService,
              private accountService: AccountService,
              private modalService: NgbModal,
              private toastr: ToastrService) {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user: User) => {
      this.loggedInUsername = user.username;
    });
  }

  ngOnInit(): void {
    this.loadMembers();
  }

  loadMembers() {
    this.loadingMembers = true;
    this.memberService.getMembers().subscribe(members => {
      this.members = members.filter(member => member.username !== this.loggedInUsername);
      this.loadingMembers = false;
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

  openEditLibraryAccess(member: Member) {
    const modalRef = this.modalService.open(LibraryAccessModalComponent);
    modalRef.componentInstance.member = member;
    modalRef.closed.subscribe((closeResult: any) => {
      console.log('Closed Result', closeResult);
    });
  }

  deleteUser(member: Member) {
    // TODO: Use a modal for this confirm
    if (confirm('Are you sure you want to delete this user?')) {
      this.memberService.deleteMember(member.username).subscribe(() => {
        this.loadMembers();
        this.toastr.success(member.username + ' has been deleted.');
      });
    }
  }

  formatLibraries(member: Member) {
    if (member.libraries.length === 0) {
      return 'None';
    }

    return member.libraries.map(item => item.name + ', ');
  }
}
