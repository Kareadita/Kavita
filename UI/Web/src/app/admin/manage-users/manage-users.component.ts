import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal, TrackByFunction} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from '@openng/ngx-toastr';
import {ResetPasswordModalComponent} from '../_modals/reset-password-modal/reset-password-modal.component';
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
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {UtcToLocalDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
import {RoleLocalizedPipe} from "../../_pipes/role-localized.pipe";
import {SettingsService} from "../settings.service";
import {ServerSettings} from "../_models/server-settings";
import {IdentityProvider} from "../../_models/user/user";
import {ImageComponent} from "../../shared/image/image.component";
import {EmptyStateComponent} from "../../shared/_components/empty-state/empty-state.component";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {
  DataTableColumnCellDirective,
  DataTableColumnDirective,
  DataTableColumnHeaderDirective,
  DatatableComponent
} from "@siemens/ngx-datatable";
import {ModalService} from "../../_services/modal.service";
import {AccountService, Role} from "../../_services/account.service";
import {MemberService} from "../../_services/member.service";
import {ConfirmService} from "../../shared/confirm.service";
import {MessageHubService} from "../../_services/message-hub.service";
import {Member} from "../../_models/auth/member";
import {TimeDifferencePipe} from "../../_pipes/time-difference.pipe";

@Component({
  selector: 'app-manage-users',
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, TagBadgeComponent, AsyncPipe, TitleCasePipe, TranslocoModule, DefaultDatePipe, NgClass,
    DefaultValuePipe, UtcToLocalTimePipe, LoadingComponent, SentenceCasePipe, UtcToLocalDatePipe,
    RoleLocalizedPipe, ImageComponent, EmptyStateComponent, ResponsiveTableComponent, NgTemplateOutlet, DatatableComponent,
    DataTableColumnDirective, DataTableColumnCellDirective, DataTableColumnHeaderDirective, RouterLink, TimeDifferencePipe]
})
export class ManageUsersComponent implements OnInit {

  protected readonly Role = Role;

  private readonly translocoService = inject(TranslocoService);
  private readonly memberService = inject(MemberService);
  protected readonly accountService = inject(AccountService);
  private readonly settingsService = inject(SettingsService);
  private readonly modalService = inject(ModalService);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly messageHub = inject(MessageHubService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly members = signal<Member[]>([]);
  settings: ServerSettings | undefined = undefined;
  protected readonly oidcSyncEnabled = signal(false);
  loggedInUsername = this.accountService.username;
  protected readonly isLoading = signal(true);
  protected readonly libraryCount = signal(0);

  trackByMember: TrackByFunction<Member> = (_, m) =>
    `${m.username}_${m.lastActiveUtc}_${m.roles.length}`;


  ngOnInit(): void {
    this.loadMembers();

    this.settingsService.getServerSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(settings => {
      this.settings = settings;
      this.oidcSyncEnabled.set(settings.oidcConfig.syncUserSettings && settings.oidcConfig.enabled);
    });
  }


  loadMembers() {
    this.isLoading.set(true);
    this.memberService.getMembers(true).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(members => {
      // Show logged-in user at the top of the list
      const sorted = [...members].sort((a: Member, b: Member) => {
        if (a.username === this.loggedInUsername()) return -1;
        if (b.username === this.loggedInUsername()) return 1;

        const nameA = a.username.toUpperCase();
        const nameB = b.username.toUpperCase();

        if (nameA < nameB) return -1;
        if (nameA > nameB) return 1;
        return 0;
      });

      this.members.set(sorted);

      // Get the admin and get their library count
      this.libraryCount.set(sorted.filter(m => this.hasAdminRole(m))[0]?.libraries.length ?? 0);

      this.isLoading.set(false);
    });
  }

  isMemberYou(member: Member): boolean {
    return this.loggedInUsername() === member.username;
  }

  openEditUser(member: Member) {
    if (!this.settings) return;

    const modalRef = this.modalService.open(EditUserComponent);
    modalRef.setInput('member', member);
    modalRef.setInput('settings', this.settings);
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
    const modalRef = this.modalService.open(InviteUserComponent);
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
    const modalRef = this.modalService.open(ResetPasswordModalComponent);
    modalRef.setInput('member', member);
  }

  hasAdminRole(member: Member) {
    return member.roles.indexOf(Role.Admin) >= 0;
  }

  getRoles(member: Member) {
    return member.roles.filter(item => item != 'Pleb');
  }

  protected readonly IdentityProvider = IdentityProvider;
}
