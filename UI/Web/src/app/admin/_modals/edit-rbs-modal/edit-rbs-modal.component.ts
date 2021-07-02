import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Member } from 'src/app/_models/member';
import { AccountService } from 'src/app/_services/account.service';
import { MemberService } from 'src/app/_services/member.service';

@Component({
  selector: 'app-edit-rbs-modal',
  templateUrl: './edit-rbs-modal.component.html',
  styleUrls: ['./edit-rbs-modal.component.scss']
})
export class EditRbsModalComponent implements OnInit {

  @Input() member: Member | undefined;
  allRoles: string[] = [];
  selectedRoles: Array<{selected: boolean, data: string}> = [];

  constructor(public modal: NgbActiveModal, private accountService: AccountService, private memberService: MemberService) { }

  ngOnInit(): void {
    this.accountService.getRoles().subscribe(roles => {
      roles = roles.filter(item => item != 'Admin' && item != 'Pleb'); // Do not allow the user to modify Account RBS
      this.allRoles = roles;
      this.selectedRoles = roles.map(item => {
        return {selected: false, data: item};
      });

      this.preselect();
    });
  }

  close() {
    this.modal.close(false);
  }

  save() {
    if (this.member?.username === undefined) {
      return;
    }

    const selectedRoles = this.selectedRoles.filter(item => item.selected).map(item => item.data);
    this.memberService.updateMemberRoles(this.member?.username, selectedRoles).subscribe(() => {
      if (this.member) {
        this.member.roles = selectedRoles;
      }
      this.modal.close(true);
    });
  }

  reset() {
    this.selectedRoles = this.allRoles.map(item => {
      return {selected: false, data: item};
    });


    this.preselect();
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

}
