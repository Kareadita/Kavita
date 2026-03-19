import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from '@jsverse/transloco';
import {CblService} from '../../../_services/cbl.service';
import {RemapRule} from '../../../_models/reading-list/cbl/remap-rule';

@Component({
  selector: 'app-manage-remap-rules-modal',
  imports: [
    TranslocoDirective,
  ],
  templateUrl: './manage-remap-rules-modal.component.html',
  styleUrl: './manage-remap-rules-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageRemapRulesModalComponent implements OnInit {
  private readonly modal = inject(NgbActiveModal);
  private readonly cblService = inject(CblService);

  rules = signal<RemapRule[]>([]);
  hasModifications = false;

  ngOnInit() {
    this.cblService.getRemapRules().subscribe(rules => this.rules.set(rules));
  }

  deleteRule(rule: RemapRule) {
    this.cblService.deleteRemapRule(rule.id).subscribe(() => {
      this.rules.set(this.rules().filter(r => r.id !== rule.id));
      this.hasModifications = true;
    });
  }

  close() {
    this.modal.close(this.hasModifications);
  }
}
