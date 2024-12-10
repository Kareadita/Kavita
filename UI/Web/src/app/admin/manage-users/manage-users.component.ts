import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {NgbModal, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {take} from 'rxjs/operators';
import {MemberService} from 'src/app/_services/member.service';
import {Member} from 'src/app/_models/auth/member';
import {AccountService} from 'src/app/_services/account.service';
import {ToastrService} from 'ngx-toastr';
import {ResetPasswordModalComponent} from '../_modals/reset-password-modal/reset-password-modal.component';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {MessageHubService} from 'src/app/_services/message-hub.service';
import {InviteUserComponent} from '../invite-user/invite-user.component';
import {EditUserComponent} from '../edit-user/edit-user.component';
import {Router} from '@angular/router';
import {TagBadgeComponent} from '../../shared/tag-badge/tag-badge.component';
import {AsyncPipe, DatePipe, NgClass, NgIf, TitleCasePipe} from '@angular/common';
import {translate, TranslocoModule, TranslocoService} from "@jsverse/transloco";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {makeBindingParser} from "@angular/compiler";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {TimeAgoPipe} from "../../_pipes/time-ago.pipe";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {DefaultModalOptions} from "../../_models/default-modal-options";

@Component({
    selector: 'app-manage-users',
    templateUrl: './manage-users.component.html',
    styleUrls: ['./manage-users.component.scss'],
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, TagBadgeComponent, AsyncPipe, TitleCasePipe, DatePipe, TranslocoModule, DefaultDatePipe, NgClass, DefaultValuePipe, ReadMoreComponent, UtcToLocalTimePipe, LoadingComponent, NgIf, TimeAgoPipe, SentenceCasePipe]
})
export class ManageUsersComponent implements OnInit {

  members: Member[] = [];
  loggedInUsername = '';
  loadingMembers = false;

  private readonly translocoService = inject(TranslocoService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly memberService = inject(MemberService);
  private readonly accountService = inject(AccountService);
  private readonly modalService = inject(NgbModal);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  public readonly messageHub = inject(MessageHubService);
  private readonly router = inject(Router);

  constructor() {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user) => {
      if (user) {
        this.loggedInUsername = user.username;
        this.cdRef.markForCheck();
      }
    });
  }

  ngOnInit(): void {
    this.loadMembers();
  }


  loadMembers() {
    this.loadingMembers = true;
    this.cdRef.markForCheck();
    this.memberService.getMembers(true).subscribe(members => {
      this.members = members;
      // Show logged-in user at the top of the list
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
      this.cdRef.markForCheck();
    });
  }

  canEditMember(member: Member): boolean {
    return this.loggedInUsername !== member.username;
  }

  openEditUser(member: Member) {
    const modalRef = this.modalService.open(EditUserComponent, DefaultModalOptions);
    modalRef.componentInstance.member = member;
    modalRef.closed.subscribe(() => {
      this.loadMembers();
    });
  }


  async deleteUser(member: Member) {
    if (await this.confirmService.confirm(this.translocoService.translate('toasts.confirm-delete-user'))) {
      this.memberService.deleteMember(member.username).subscribe(() => {
        setTimeout(() => {
          this.loadMembers();
          this.toastr.success(this.translocoService.translate('toasts.user-deleted', {user: member.username}));
        }, 30); // SetTimeout because I've noticed this can run super fast and not give enough time for data to flush
      });
    }
  }

  inviteUser() {
    const modalRef = this.modalService.open(InviteUserComponent, DefaultModalOptions);
    modalRef.closed.subscribe((successful: boolean) => {
      this.loadMembers();
    });
  }

  resendEmail(member: Member) {
    this.accountService.resendConfirmationEmail(member.id).subscribe(async (response) => {
      if (response.emailSent) {
        this.toastr.info(this.translocoService.translate('toasts.email-sent', {email: member.username}));
        return;
      }
      await this.confirmService.alert(
        this.translocoService.translate('toasts.click-email-link') + '<br/> <a href="' + response.emailLink + '" target="_blank" rel="noopener noreferrer">' + response.emailLink + '</a>');
    });
  }

  setup(member: Member) {
    this.accountService.getInviteUrl(member.id, false).subscribe(url => {
      if (url) {
        this.router.navigateByUrl(url);
      }
    });
  }

  updatePassword(member: Member) {
    const modalRef = this.modalService.open(ResetPasswordModalComponent, DefaultModalOptions);
    modalRef.componentInstance.member = member;
  }

  formatLibraries(member: Member) {
    if (member.libraries.length === 0) {
      return translate('manage-users.none');
    }

    return member.libraries.map(item => item.name).join(', ');
  }

  hasAdminRole(member: Member) {
    return member.roles.indexOf('Admin') >= 0;
  }

  getRoles(member: Member) {
    return member.roles.filter(item => item != 'Pleb');
  }

  protected readonly makeBindingParser = makeBindingParser;
}
