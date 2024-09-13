import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import { Member } from 'src/app/_models/auth/member';
import { User } from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { NgFor } from '@angular/common';
import {TranslocoDirective,} from "@jsverse/transloco";
import {SelectionModel} from "../../typeahead/_models/selection-model";

@Component({
    selector: 'app-role-selector',
    templateUrl: './role-selector.component.html',
    styleUrls: ['./role-selector.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgFor, ReactiveFormsModule, FormsModule, TranslocoDirective]
})
export class RoleSelectorComponent implements OnInit {

  private readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);


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
  selections!: SelectionModel<string>;
  selectAll: boolean = false;

  get hasSomeSelected() {
    return this.selections != null && this.selections.hasSomeSelected();
  }

  ngOnInit(): void {
    this.accountService.getRoles().subscribe(roles => {
      const bannedRoles = ['Pleb'];
      if (!this.allowAdmin) {
        bannedRoles.push('Admin');
      }
      roles = roles.filter(item => !bannedRoles.includes(item));
      this.allRoles = roles;
      this.selections = new SelectionModel<string>(false, this.allRoles);

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
    this.syncSelections();
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
    this.syncSelections();
    this.cdRef.markForCheck();
    this.selected.emit(roles);
  }

  syncSelections() {
    this.selectedRoles.forEach(s => this.selections.toggle(s.data, s.selected));
    this.cdRef.markForCheck();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;

    // Update selectedRoles considering disabled state
    this.selectedRoles.filter(r => !r.disabled).forEach(r => r.selected = this.selectAll);

    // Sync selections with updated selectedRoles
    this.syncSelections();

    this.selected.emit(this.selections.selected());
    this.cdRef.markForCheck();
  }

}
