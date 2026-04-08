import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {CblService} from '../../_services/cbl.service';
import {AccountService} from '../../_services/account.service';
import {ConfirmService} from '../../shared/confirm.service';
import {ToastrService} from 'ngx-toastr';
import {RemapRule} from '../../_models/reading-list/cbl/remap-rule';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {NgxDatatableModule} from '@siemens/ngx-datatable';
import {ResponsiveTableComponent} from '../../shared/_components/responsive-table/responsive-table.component';
import {DatePipe} from '@angular/common';
import {DefaultValuePipe} from '../../_pipes/default-value.pipe';
import {CblRemapRuleChapterTitlePipe} from '../../_pipes/cbl-remap-rule-chapter-title.pipe';
import {EditRemapRuleComponent} from './edit-remap-rule/edit-remap-rule.component';

@Component({
  selector: 'app-manage-remap-rules',
  templateUrl: './manage-remap-rules.component.html',
  styleUrls: ['./manage-remap-rules.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, NgxDatatableModule, ResponsiveTableComponent, DatePipe, DefaultValuePipe, CblRemapRuleChapterTitlePipe, EditRemapRuleComponent]
})
export class ManageRemapRulesComponent implements OnInit {

  private readonly cblService = inject(CblService);
  private readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);

  rules = signal<RemapRule[]>([]);
  isAdmin = this.accountService.hasAdminRole;
  isReadOnly = this.accountService.hasReadOnlyRole;
  currentUserId = computed(() => this.accountService.currentUser()?.id ?? 0);

  showForm = signal(false);
  editingRule = signal<RemapRule | null>(null);
  isEditing = computed(() => this.editingRule() !== null);

  myRules = computed(() => {
    const userId = this.currentUserId();
    return this.rules().filter(r => r.appUserId === userId && !r.isGlobal);
  });

  globalRules = computed(() => this.rules().filter(r => r.isGlobal));

  otherUserRules = computed(() => {
    const userId = this.currentUserId();
    return this.rules().filter(r => r.appUserId !== userId && !r.isGlobal);
  });

  trackBy = (_idx: number, item: RemapRule) => item.id;

  ngOnInit() {
    this.loadRules();
  }

  loadRules() {
    const obs = this.isAdmin() ? this.cblService.getAllRemapRules() : this.cblService.getRemapRules();
    obs.subscribe(rules => this.rules.set(rules));
  }

  openCreateForm() {
    this.editingRule.set(null);
    this.showForm.set(true);
  }

  editRule(rule: RemapRule) {
    this.editingRule.set(rule);
    this.showForm.set(true);
  }

  onRuleSaved(rule: RemapRule) {
    const editing = this.editingRule();
    if (editing) {
      this.rules.update(rules => rules.map(r => r.id === editing.id ? rule : r));
    } else {
      this.rules.update(rules => [...rules, rule]);
      this.toastr.success(translate('toasts.cbl-remap-rule-created'));
    }

    this.closeForm();
  }

  onRuleCancelled() {
    this.closeForm();
  }

  async deleteRule(rule: RemapRule) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-cbl-remap-rule'))) return;
    this.cblService.deleteRemapRule(rule.id).subscribe(() => {
      this.rules.update(rules => rules.filter(r => r.id !== rule.id));
      this.toastr.success(translate('toasts.cbl-remap-rule-deleted'));
    });
  }

  promoteRule(rule: RemapRule) {
    this.cblService.promoteRule(rule.id).subscribe(updated => {
      this.rules.update(rules => rules.map(r => r.id === updated.id ? updated : r));
      this.toastr.success(translate('toasts.cbl-remap-rule-promoted'));
    });
  }

  demoteRule(rule: RemapRule) {
    this.cblService.demoteRule(rule.id).subscribe(updated => {
      this.rules.update(rules => rules.map(r => r.id === updated.id ? updated : r));
      this.toastr.success(translate('toasts.cbl-remap-rule-demoted'));
    });
  }

  private closeForm() {
    this.showForm.set(false);
    this.editingRule.set(null);
  }
}
