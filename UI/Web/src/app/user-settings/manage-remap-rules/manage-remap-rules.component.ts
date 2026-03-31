import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {CblService} from '../../_services/cbl.service';
import {AccountService} from '../../_services/account.service';
import {ConfirmService} from '../../shared/confirm.service';
import {ToastrService} from 'ngx-toastr';
import {SearchService} from '../../_services/search.service';
import {UtilityService} from '../../shared/_services/utility.service';
import {RemapRule} from '../../_models/reading-list/cbl/remap-rule';
import {SearchResult} from '../../_models/search/search-result';
import {TypeaheadSettings} from '../../typeahead/_models/typeahead-settings';
import {map} from 'rxjs';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {NgxDatatableModule} from '@siemens/ngx-datatable';
import {ResponsiveTableComponent} from '../../shared/_components/responsive-table/responsive-table.component';
import {TypeaheadComponent} from '../../typeahead/_components/typeahead.component';
import {NonNullableFormBuilder, ReactiveFormsModule} from '@angular/forms';
import {DatePipe} from '@angular/common';

@Component({
  selector: 'app-manage-remap-rules',
  templateUrl: './manage-remap-rules.component.html',
  styleUrls: ['./manage-remap-rules.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, NgxDatatableModule, ResponsiveTableComponent, TypeaheadComponent, ReactiveFormsModule, DatePipe]
})
export class ManageRemapRulesComponent implements OnInit {

  private readonly cblService = inject(CblService);
  private readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  private readonly searchService = inject(SearchService);
  private readonly utilityService = inject(UtilityService);
  private readonly fb = inject(NonNullableFormBuilder);

  rules = signal<RemapRule[]>([]);
  isAdmin = this.accountService.hasAdminRole;
  isReadOnly = this.accountService.hasReadOnlyRole;
  currentUserId = computed(() => this.accountService.currentUser()?.id ?? 0);

  showCreateForm = signal(false);
  createForm = this.fb.group({
    cblSeriesName: '',
    cblVolume: '',
  });
  selectedSeries = signal<SearchResult | null>(null);
  seriesSettings: TypeaheadSettings<SearchResult>;

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

  constructor() {
    this.seriesSettings = new TypeaheadSettings<SearchResult>();
    this.seriesSettings.minCharacters = 2;
    this.seriesSettings.multiple = false;
    this.seriesSettings.id = 'remap-series';
    this.seriesSettings.unique = true;
    this.seriesSettings.addIfNonExisting = false;
    this.seriesSettings.fetchFn = (filter: string) =>
      this.searchService.search(filter).pipe(
        map(group => group.series),
        map(items => this.seriesSettings.compareFn(items, filter)),
      );
    this.seriesSettings.trackByIdentityFn = (_idx, item) => item.seriesId + '';
    this.seriesSettings.compareFn = (options: SearchResult[], filter: string) => {
      return options.filter(m => {
        return this.utilityService.filter(m.name, filter) || this.utilityService.filter(m.localizedName, filter);
      });
    };
    this.seriesSettings.selectionCompareFn = (a: SearchResult, b: SearchResult) => {
      return a.seriesId === b.seriesId;
    };
  }

  ngOnInit() {
    this.loadRules();
  }

  loadRules() {
    const obs = this.isAdmin() ? this.cblService.getAllRemapRules() : this.cblService.getRemapRules();
    obs.subscribe(rules => this.rules.set(rules));
  }

  onSeriesSelected(event: SearchResult[]) {
    this.selectedSeries.set(event.length > 0 ? event[0] : null);
  }

  toggleCreateForm() {
    this.showCreateForm.update(v => !v);
    if (!this.showCreateForm()) {
      this.resetCreateForm();
    }
  }

  resetCreateForm() {
    this.createForm.reset();
    this.selectedSeries.set(null);
  }

  createRule() {
    const {cblSeriesName, cblVolume} = this.createForm.value;
    const selectedSeries = this.selectedSeries();
    if (!cblSeriesName?.trim() || !selectedSeries) return;


    const issueDetail = cblVolume?.trim() ? { cblVolume: cblVolume.trim() } : undefined;
    this.cblService.createRemapRule(cblSeriesName.trim(), selectedSeries.seriesId, issueDetail).subscribe(rule => {
      this.rules.update(rules => [...rules, rule]);
      this.showCreateForm.set(false);
      this.resetCreateForm();
      this.toastr.success(translate('toasts.cbl-remap-rule-created'));
    });
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
}
