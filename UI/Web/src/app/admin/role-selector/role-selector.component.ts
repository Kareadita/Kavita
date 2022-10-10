import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Member } from 'src/app/_models/member';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { MemberService } from 'src/app/_services/member.service';

@Component({
  selector: 'app-role-selector',
  templateUrl: './role-selector.component.html',
  styleUrls: ['./role-selector.component.scss']
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
  selectedRoles: Array<{selected: boolean, data: string}> = [];

  constructor(public modal: NgbActiveModal, private accountService: AccountService, private memberService: MemberService) { }

  ngOnInit(): void {
    this.accountService.getRoles().subscribe(roles => {
      const bannedRoles = ['Pleb'];
      if (!this.allowAdmin) {
        bannedRoles.push('Admin');
      }
      roles = roles.filter(item => !bannedRoles.includes(item));
      this.allRoles = roles;
      this.selectedRoles = roles.map(item => {
        return {selected: false, data: item};
      });
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
    }
  }

  handleModelUpdate() {
    this.selected.emit(this.selectedRoles.filter(item => item.selected).map(item => item.data));
  }

}
