import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {NgxDatatableModule} from '@siemens/ngx-datatable';
import {DatePipe, APP_BASE_HREF} from '@angular/common';
import {RouterLink} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {AccountService} from '../../_services/account.service';
import {ConfirmService} from '../../shared/confirm.service';
import {KoboRemovedBook} from '../../_models/kobo/kobo-removed-book';
import {ResponsiveTableComponent} from '../../shared/_components/responsive-table/responsive-table.component';
import {UtcToLocalDatePipe} from '../../_pipes/utc-to-locale-date.pipe';
import {DefaultDatePipe} from '../../_pipes/default-date.pipe';
import {SettingsTabId} from '../../sidenav/preference-nav/preference-nav.component';
import {ImageService} from '../../_services/image.service';
import {ImageComponent} from '../../shared/image/image.component';

@Component({
  selector: 'app-removed-from-kobo',
  imports: [
    TranslocoDirective,
    NgxDatatableModule,
    ResponsiveTableComponent,
    UtcToLocalDatePipe,
    DefaultDatePipe,
    DatePipe,
    RouterLink,
    ImageComponent,
  ],
  templateUrl: './removed-from-kobo.component.html',
  styleUrl: './removed-from-kobo.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RemovedFromKoboComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  protected readonly imageService = inject(ImageService);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  isReadOnly = this.accountService.hasReadOnlyRole;
  isLoading = signal(true);
  books = signal<KoboRemovedBook[]>([]);
  selectedIds = signal<Set<number>>(new Set());

  selectedCount = computed(() => this.selectedIds().size);
  allSelected = computed(() => {
    const rows = this.books();
    return rows.length > 0 && this.selectedIds().size === rows.length;
  });

  trackBy = (_: number, item: KoboRemovedBook) => item.chapterId;
  protected readonly clientsTab = SettingsTabId.Clients;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading.set(true);
    this.accountService.getRemovedKoboBooks().subscribe({
      next: (data) => {
        this.books.set(data);
        this.selectedIds.set(new Set());
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toastr.error(err?.error || translate('errors.generic'));
      },
    });
  }

  isSelected(chapterId: number) {
    return this.selectedIds().has(chapterId);
  }

  toggle(chapterId: number) {
    const next = new Set(this.selectedIds());
    if (next.has(chapterId)) {
      next.delete(chapterId);
    } else {
      next.add(chapterId);
    }
    this.selectedIds.set(next);
  }

  toggleAll() {
    if (this.allSelected()) {
      this.selectedIds.set(new Set());
      return;
    }
    this.selectedIds.set(new Set(this.books().map(b => b.chapterId)));
  }

  async restoreSelected() {
    const ids = [...this.selectedIds()];
    if (ids.length === 0) return;
    if (!await this.confirmService.confirm(translate('toasts.confirm-restore-selected-kobo-books'))) {
      return;
    }
    this.accountService.restoreRemovedKoboBooks(ids).subscribe({
      next: () => {
        this.toastr.success(translate('toasts.kobo-restore-removed-books'));
        this.loadData();
      },
      error: (err) => {
        this.toastr.error(err?.error || translate('errors.generic'));
      },
    });
  }

  async restoreAll() {
    if (this.books().length === 0) return;
    if (!await this.confirmService.confirm(translate('toasts.confirm-restore-removed-kobo-books'))) {
      return;
    }
    this.accountService.restoreRemovedKoboBooks().subscribe({
      next: () => {
        this.toastr.success(translate('toasts.kobo-restore-removed-books'));
        this.loadData();
      },
      error: (err) => {
        this.toastr.error(err?.error || translate('errors.generic'));
      },
    });
  }
}
