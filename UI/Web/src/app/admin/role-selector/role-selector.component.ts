import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Member } from 'src/app/_models/auth/member';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { MemberService } from 'src/app/_services/member.service';

@Component({
  selector: 'app-role-selector',
  templateUrl: './role-selector.component.html',
  styleUrls: ['./role-selector.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoleSelectorComponent implements OnInit {

  /**
   * This must have roles
   */
  @Input() member: Member | undefined | User;
  /**
   * Allows the selection of Admin role
   */
  @Input() allowAdmin: boolean = false;
  @Output() selected: EventEmitter<string[]> = new EventEmitter<string[]>();

  allRoles: string[] = [];
  selectedRoles: Array<{selected: boolean, disabled: boolean, data: string}> = [];

  constructor(public modal: NgbActiveModal, private accountService: AccountService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.accountService.getRoles().subscribe(roles => {
      const bannedRoles = ['Pleb'];
      if (!this.allowAdmin) {
        bannedRoles.push('Admin');
      }
      roles = roles.filter(item => !bannedRoles.includes(item));
      this.allRoles = roles;
      this.selectedRoles = roles.map(item => {
        return {selected: false, disabled: false, data: item};
      });
      this.cdRef.markForCheck();
      this.preselect();
      this.selected.emit(this.selectedRoles.filter(item => item.selected).map(item => item.data));
    });
  }

  preselect() {
    if (this.member !== undefined) {
      this.member.roles.forEach(role => {
        const foundRole = this.selectedRoles.filter(item => item.data === role);
        if (foundRole.length > 0) {
          foundRole[0].selected = true;
        }
      });
    } else {
      // For new users, preselect LoginRole
      this.selectedRoles.forEach(role => {
        if (role.data == 'Login') {
          role.selected = true;
        }
      });
    }
    this.cdRef.markForCheck();
  }

  handleModelUpdate() {
    const roles = this.selectedRoles.filter(item => item.selected).map(item => item.data);
    if (roles.filter(r => r === 'Admin').length > 0) {
      // Disable all other items as Admin is selected
      this.selectedRoles.filter(item => item.data !== 'Admin').forEach(e => {
        e.disabled = true;
      });
    } else {
      // Re-enable everything
      this.selectedRoles.forEach(e => {
        e.disabled = false;
      });
    }
    this.cdRef.markForCheck();
    this.selected.emit(roles);
  }

}
