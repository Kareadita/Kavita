import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {CblService} from '../../../_services/cbl.service';
import {AccountService} from '../../../_services/account.service';
import {RemapRule} from '../../../_models/reading-list/cbl/remap-rule';
import {CblRemapRuleKind} from '../../../_models/reading-list/cbl/cbl-remap-rule-kind.enum';
import {EntityTitleComponent} from '../../../cards/entity-title/entity-title.component';
import {ConfirmService} from "../../../shared/confirm.service";

@Component({
  selector: 'app-manage-remap-rules-modal',
  imports: [
    TranslocoDirective,
    EntityTitleComponent,
  ],
  templateUrl: './manage-remap-rules-modal.component.html',
  styleUrl: './manage-remap-rules-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageRemapRulesModalComponent implements OnInit {
  private readonly modal = inject(NgbActiveModal);
  protected readonly cblService = inject(CblService);
  protected readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly CblRemapRuleKind = CblRemapRuleKind;

  rules = signal<RemapRule[]>([]);
  hasModifications = false;
  currentUserId = computed(() => this.accountService.currentUser()?.id ?? 0);

  sortedRules = computed(() => {
    const userId = this.currentUserId();
    return [...this.rules()].sort((a, b) => {
      // User's own rules first, global last
      const aIsOwn = a.appUserId === userId && !a.isGlobal;
      const bIsOwn = b.appUserId === userId && !b.isGlobal;
      if (aIsOwn !== bIsOwn) return aIsOwn ? -1 : 1;

      const aIsGlobal = a.isGlobal;
      const bIsGlobal = b.isGlobal;
      if (aIsGlobal !== bIsGlobal) return aIsGlobal ? 1 : -1;

      // Within same group, most recently created first
      return new Date(b.createdUtc).getTime() - new Date(a.createdUtc).getTime();
    });
  });

  ngOnInit() {
    this.cblService.getRemapRules().subscribe(rules => this.rules.set(rules));
  }

  async deleteRule(rule: RemapRule) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-cbl-remap-rule'))) return;

    this.cblService.deleteRemapRule(rule.id).subscribe(() => {
      this.rules.set(this.rules().filter(r => r.id !== rule.id));
      this.hasModifications = true;
    });
  }

  close() {
    this.modal.close(this.hasModifications);
  }
}
