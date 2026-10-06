import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, output, signal} from '@angular/core';
import {filter, Observable} from 'rxjs';
import {allMediaErrorReasons, KavitaMediaError, MediaErrorReason} from '../_models/media-error';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";
import {ServerService} from "../../_services/server.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {FilterFieldComponent} from "../../shared/_components/filter-field/filter-field.component";
import {matchesQuery} from "../../_helpers/filtered";
import {SettingSelectComponent} from "../../settings/_components/setting-enum-select/setting-select.component";
import {form, FormField} from "@angular/forms/signals";
import {MediaErrorReasonPipe} from "../../_pipes/media-error-reason.pipe";
import {TranslocoInjectComponent} from "../../shared/_components/transloco-inject/transloco-inject.component";
import {TranslocoSlotDirective} from "../../_directives/transloco-slot.directive";
import {MediaIssuesTableComponent} from "./media-issues-table/media-issues-table.component";
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ConfirmService} from "../../shared/confirm.service";
import {EmptyStateComponent} from "../../shared/_components/empty-state/empty-state.component";

interface FormModel {
  libraryId: number | null;
  reason: MediaErrorReason | null;
}

@Component({
  selector: 'app-manage-media-issues',
  templateUrl: './manage-media-issues.component.html',
  styleUrl: './manage-media-issues.component.scss',
  imports: [TranslocoDirective, FilterFieldComponent, SettingSelectComponent, FormField, MediaErrorReasonPipe,
    TranslocoInjectComponent, TranslocoSlotDirective, MediaIssuesTableComponent, NgbAccordionDirective, NgbAccordionItem,
    NgbAccordionHeader, NgbAccordionButton, NgbAccordionCollapse, NgbAccordionBody, LoadingComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageMediaIssuesComponent implements OnInit {

  readonly alertCount = output<number>();

  private readonly serverService = inject(ServerService);
  private readonly messageHub = inject(MessageHubService);
  private readonly confirmService = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected data = signal<KavitaMediaError[]>([]);
  protected isLoading = signal(true);
  protected filterQuery = signal('');
  private readonly formModel = signal<FormModel>({
    libraryId: null,
    reason: null,
  });
  protected readonly formGroup = form(this.formModel);

  /** Maps libraryId -> name */
  protected readonly libraryNames = computed(() => {
    const names = new Map<number, string>();
    for (const issue of this.data()) {
      if (issue.libraryId != null && issue.libraryName) {
        names.set(issue.libraryId, issue.libraryName);
      }
    }
    return names;
  });
  protected readonly libraryOptions = computed(() => [...this.libraryNames().entries()]
    .sort((a, b) => a[1].localeCompare(b[1]))
    .map(([id]) => id));

  private readonly filteredData = computed(() => {
    const {libraryId, reason} = this.formModel();
    const query = this.filterQuery();

    return this.data().filter(d => (libraryId === null || d.libraryId === libraryId)
      && (reason === null || d.reason === reason)
      && matchesQuery(d, query, 'filePath', 'seriesName', 'libraryName', 'details'));
  });
  protected readonly activeIssues = computed(() => this.filteredData().filter(d => !d.isDismissed));
  protected readonly hasActiveIssues = computed(() => this.data().some(d => !d.isDismissed));
  protected readonly dismissedIssues = computed(() => this.filteredData().filter(d => d.isDismissed));

  ngOnInit(): void {
    this.loadData();
    this.messageHub.messages$.pipe(
      filter(m => m.event === EVENTS.ScanSeries),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => this.loadData());
  }

  protected loadData() {
    this.serverService.getMediaErrors().subscribe(d => {
      this.data.set(d);
      this.isLoading.set(false);
      this.alertCount.emit(d.filter(issue => !issue.isDismissed).length);
    });
  }

  protected dismiss(ids: number[]) {
    this.reloadAfter(this.serverService.dismissMediaErrors(ids));
  }

  protected restore(ids: number[]) {
    this.reloadAfter(this.serverService.undismissMediaErrors(ids));
  }

  protected async clearAll() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-clear-media-issues'))) return;
    this.reloadAfter(this.serverService.clearMediaAlerts());
  }

  private reloadAfter(request: Observable<unknown>) {
    request.subscribe(() => this.loadData());
  }

  protected readonly WikiLink = WikiLink;
  protected readonly allMediaErrorReasons = allMediaErrorReasons;
}
