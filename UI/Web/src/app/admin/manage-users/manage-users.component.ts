import { Component, OnDestroy, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { catchError, take } from 'rxjs/operators';
import { MemberService } from 'src/app/_services/member.service';
import { Member } from 'src/app/_models/member';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { ToastrService } from 'ngx-toastr';
import { ResetPasswordModalComponent } from '../_modals/reset-password-modal/reset-password-modal.component';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { Subject } from 'rxjs';
import { MessageHubService } from 'src/app/_services/message-hub.service';
import { InviteUserComponent } from '../invite-user/invite-user.component';
import { EditUserComponent } from '../edit-user/edit-user.component';
import { ServerService } from 'src/app/_services/server.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-manage-users',
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss']
})
export class ManageUsersComponent implements OnInit, OnDestroy {

  members: Member[] = [];
  pendingInvites: Member[] = [];
  loggedInUsername = '';
  loadingMembers = false;

  private onDestroy = new Subject<void>();

  constructor(private memberService: MemberService,
              private accountService: AccountService,
              private modalService: NgbModal,
              private toastr: ToastrService,
              private confirmService: ConfirmService,
              public messageHub: MessageHubService,
              private serverService: ServerService,
              private router: Router) {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user) => {
      if (user) {
        this.loggedInUsername = user.username;
      }
    });

  }

  ngOnInit(): void {
    this.loadMembers();

    this.loadPendingInvites();
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

  loadPendingInvites() {
    this.pendingInvites = [];
    this.memberService.getPendingInvites().subscribe(members => {
      this.pendingInvites = members;
      // Show logged in user at the top of the list
      this.pendingInvites.sort((a: Member, b: Member) => {
        if (a.username === this.loggedInUsername) return 1;
        if (b.username === this.loggedInUsername) return 1;

        const nameA = a.username.toUpperCase();
        const nameB = b.username.toUpperCase();
        if (nameA < nameB) return -1;
        if (nameA > nameB) return 1;
        return 0;
      })
    });
  }

  canEditMember(member: Member): boolean {
    return this.loggedInUsername !== member.username;
  }

  openEditUser(member: Member) {
    const modalRef = this.modalService.open(EditUserComponent, {size: 'lg'});
    modalRef.componentInstance.member = member;
    modalRef.closed.subscribe(() => {
      this.loadMembers();
    });
  }
  

  async deleteUser(member: Member) {
    if (await this.confirmService.confirm('Are you sure you want to delete this user?')) {
      this.memberService.deleteMember(member.username).subscribe(() => {
        setTimeout(() => {
          this.loadMembers();
          this.loadPendingInvites();
          this.toastr.success(member.username + ' has been deleted.');
        }, 30); // SetTimeout because I've noticed this can run super fast and not give enough time for data to flush
      });
    }
  }

  inviteUser() {
    const modalRef = this.modalService.open(InviteUserComponent, {size: 'lg'});
    modalRef.closed.subscribe((successful: boolean) => {
      this.loadPendingInvites();
    });
  }

  resendEmail(member: Member) {
    this.serverService.isServerAccessible().subscribe(canAccess => {
      this.accountService.resendConfirmationEmail(member.id).subscribe(async (email) => {
        if (canAccess) {
          this.toastr.info('Email sent to ' + member.username);
          return;
        }
        await this.confirmService.alert(
          'Please click this link to confirm your email. You must confirm to be able to login. You may need to log out of the current account before clicking. <br/> <a href="' + email + '" target="_blank">' + email + '</a>');

      });
    });
  }

  setup(member: Member) {
    this.accountService.getInviteUrl(member.id, false).subscribe(url => {
      console.log('Invite Url: ', url);
      if (url) {
        this.router.navigateByUrl(url);
      }
    });
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
