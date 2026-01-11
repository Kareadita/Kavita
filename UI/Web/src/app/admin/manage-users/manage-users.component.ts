import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit, TrackByFunction} from '@angular/core';
import {NgbModal, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {take} from 'rxjs/operators';
import {MemberService} from 'src/app/_services/member.service';
import {Member} from 'src/app/_models/auth/member';
import {AccountService, Role} from 'src/app/_services/account.service';
import {ToastrService} from 'ngx-toastr';
import {ResetPasswordModalComponent} from '../_modals/reset-password-modal/reset-password-modal.component';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {MessageHubService} from 'src/app/_services/message-hub.service';
import {InviteUserComponent} from '../invite-user/invite-user.component';
import {EditUserComponent} from '../edit-user/edit-user.component';
import {Router, RouterLink} from '@angular/router';
import {TagBadgeComponent} from '../../shared/tag-badge/tag-badge.component';
import {AsyncPipe, NgClass, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {TranslocoModule, TranslocoService} from "@jsverse/transloco";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {TimeAgoPipe} from "../../_pipes/time-ago.pipe";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {DefaultModalOptions} from "../../_models/default-modal-options";
import {UtcToLocalDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
import {RoleLocalizedPipe} from "../../_pipes/role-localized.pipe";
import {SettingsService} from "../settings.service";
import {ServerSettings} from "../_models/server-settings";
import {IdentityProvider} from "../../_models/user/user";
import {ImageComponent} from "../../shared/image/image.component";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {
  DataTableColumnCellDirective,
  DataTableColumnDirective,
  DataTableColumnHeaderDirective,
  DatatableComponent
} from "@siemens/ngx-datatable";

@Component({
  selector: 'app-manage-users',
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, TagBadgeComponent, AsyncPipe, TitleCasePipe, TranslocoModule, DefaultDatePipe, NgClass,
    DefaultValuePipe, UtcToLocalTimePipe, LoadingComponent, TimeAgoPipe, SentenceCasePipe, UtcToLocalDatePipe,
    RoleLocalizedPipe, ImageComponent, ResponsiveTableComponent, NgTemplateOutlet, DatatableComponent, DataTableColumnDirective, DataTableColumnCellDirective, DataTableColumnHeaderDirective, RouterLink]
})
export class ManageUsersComponent implements OnInit {

  protected readonly Role = Role;

  private readonly translocoService = inject(TranslocoService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly memberService = inject(MemberService);
  private readonly accountService = inject(AccountService);
  private readonly settingsService = inject(SettingsService);
  private readonly modalService = inject(NgbModal);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  public readonly messageHub = inject(MessageHubService);
  private readonly router = inject(Router);

  members: Member[] = [];
  settings: ServerSettings | undefined = undefined;
  oidcSyncEnabled: boolean = false;
  loggedInUsername = '';
  loadingMembers = false;
  libraryCount: number = 0;

  trackByMember: TrackByFunction<Member> = (_, m) =>
    `${m.username}_${m.lastActiveUtc}_${m.roles.length}`;


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

    this.settingsService.getServerSettings().subscribe(settings => {
      this.settings = settings;
      this.oidcSyncEnabled = settings.oidcConfig.syncUserSettings && settings.oidcConfig.enabled;
    });
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
      });

      // Get the admin and get their library count
      this.libraryCount = this.members.filter(m => this.hasAdminRole(m))[0].libraries.length;

      this.loadingMembers = false;
      this.cdRef.markForCheck();
    });
  }

  canEditMember(member: Member): boolean {
    return this.loggedInUsername !== member.username;
  }

  openEditUser(member: Member) {
    if (!this.settings) return;

    const modalRef = this.modalService.open(EditUserComponent, DefaultModalOptions);
    modalRef.componentInstance.member.set(member);
    modalRef.componentInstance.settings.set(this.settings);
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
        }, 30); // SetTimeout because I've noticed this can run superfast and not give enough time for data to flush
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

  hasAdminRole(member: Member) {
    return member.roles.indexOf(Role.Admin) >= 0;
  }

  getRoles(member: Member) {
    return member.roles.filter(item => item != 'Pleb');
  }

  protected readonly IdentityProvider = IdentityProvider;
}
