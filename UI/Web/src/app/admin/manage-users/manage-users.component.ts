import { Component, OnDestroy, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { MemberService } from 'src/app/_services/member.service';
import { Member } from 'src/app/_models/member';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { LibraryAccessModalComponent } from '../_modals/library-access-modal/library-access-modal.component';
import { ToastrService } from 'ngx-toastr';
import { ResetPasswordModalComponent } from '../_modals/reset-password-modal/reset-password-modal.component';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { EditRbsModalComponent } from '../_modals/edit-rbs-modal/edit-rbs-modal.component';
import { Subject } from 'rxjs';
import { MessageHubService } from 'src/app/_services/message-hub.service';

@Component({
  selector: 'app-manage-users',
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss']
})
export class ManageUsersComponent implements OnInit, OnDestroy {

  members: Member[] = [];
  loggedInUsername = '';

  // Create User functionality
  createMemberToggle = false;
  loadingMembers = false;

  private onDestroy = new Subject<void>();

  constructor(private memberService: MemberService,
              private accountService: AccountService,
              private modalService: NgbModal,
              private toastr: ToastrService,
              private confirmService: ConfirmService,
              public messageHub: MessageHubService) {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user: User) => {
      this.loggedInUsername = user.username;
    });

  }

  ngOnInit(): void {
    this.loadMembers();
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  loadMembers() {
    this.loadingMembers = true;
    this.memberService.getMembers().subscribe(members => {
      this.members = members;
      // Show logged in user at the top of the list
      this.members.sort((a: Member, b: Member) => {
        if (a.username === this.loggedInUsername) return 1;
        if (b.username === this.loggedInUsername) return 1;

        const nameA = a.username.toUpperCase();
        const nameB = b.username.toUpperCase();
        if (nameA < nameB) return -1;
        if (nameA > nameB) return 1;
        return 0;
      })
      this.loadingMembers = false;
    });
  }

  canEditMember(member: Member): boolean {
    return this.loggedInUsername !== member.username;
  }

  createMember() {
    this.createMemberToggle = true;
  }

  onMemberCreated(createdUser: User | null) {
    this.createMemberToggle = false;
    this.loadMembers();
  }

  openEditLibraryAccess(member: Member) {
    const modalRef = this.modalService.open(LibraryAccessModalComponent);
    modalRef.componentInstance.member = member;
    modalRef.closed.subscribe(() => {
      this.loadMembers();
    });
  }

  async deleteUser(member: Member) {
    if (await this.confirmService.confirm('Are you sure you want to delete this user?')) {
      this.memberService.deleteMember(member.username).subscribe(() => {
        this.loadMembers();
        this.toastr.success(member.username + ' has been deleted.');
      });
    }
  }

  openEditRole(member: Member) {
    const modalRef = this.modalService.open(EditRbsModalComponent);
    modalRef.componentInstance.member = member;
    modalRef.closed.subscribe((updatedMember: Member) => {
      if (updatedMember !== undefined) {
        member = updatedMember;
      }
    })
  }

  updatePassword(member: Member) {
    const modalRef = this.modalService.open(ResetPasswordModalComponent);
    modalRef.componentInstance.member = member;
  }

  formatLibraries(member: Member) {
    if (member.libraries.length === 0) {
      return 'None';
    }

    return member.libraries.map(item => item.name).join(', ');
  }

  hasAdminRole(member: Member) {
    return member.roles.indexOf('Admin') >= 0;
  }

  getRoles(member: Member) {
    return member.roles.filter(item => item != 'Pleb');
  }
}
