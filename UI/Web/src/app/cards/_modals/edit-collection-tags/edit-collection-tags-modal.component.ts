import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal} from '@angular/core';
import {NgbActiveModal, NgbPagination, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from '@openng/ngx-toastr';
import {concat, delay, forkJoin, last, Observable, tap} from 'rxjs';
import {DecimalPipe, NgTemplateOutlet} from "@angular/common";
import {CoverImageChooserComponent} from "../../cover-image-chooser/cover-image-chooser.component";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../../_services/cover-chooser-config-factory.service";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {matchesQuery} from "../../../_helpers/filtered";
import {AccountService} from "../../../_services/account.service";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {Tracker} from "../../../shared/utils/Tracker";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {UtilityService} from "../../../shared/_services/utility.service";
import {SeriesService} from "../../../_services/series.service";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {ConfirmService} from "../../../shared/confirm.service";
import {LibraryService} from "../../../_services/library.service";
import {UploadService} from "../../../_services/upload.service";
import {UserCollection} from "../../../_models/collection-tag";
import {Pagination} from "../../../_models/pagination";
import {Series} from "../../../_models/series";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {disabled, form, FormField, required, validateAsync} from "@angular/forms/signals";
import {rxResource} from "@angular/core/rxjs-interop";
import {FilterFieldComponent} from "../../../shared/_components/filter-field/filter-field.component";
import {EditModalShellComponent} from "../../../shared/edit-modal-shell/edit-modal-shell.component";
import {EditTabDirective} from "../../../shared/_directive/edit-tab.directive";

interface FormModel {
  title: string;
  summary: string;
  coverImageLocked: boolean;
  promoted: boolean;
}

@Component({
  selector: 'app-edit-collection-tags',
  imports: [NgbPagination, CoverImageChooserComponent, NgbTooltip, TranslocoDirective, NgTemplateOutlet,
    DefaultDatePipe, SafeHtmlPipe, SafeUrlPipe, DecimalPipe, UtcToLocalTimePipe, ValidationErrorsComponent,
    FormFieldDirective, FormField, FilterFieldComponent, EditModalShellComponent, EditTabDirective],
  templateUrl: './edit-collection-tags-modal.component.html',
  styleUrls: ['./edit-collection-tags-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditCollectionTagsModalComponent implements OnInit {

  public readonly modal = inject(NgbActiveModal);
  public readonly utilityService = inject(UtilityService);
  private readonly seriesService = inject(SeriesService);
  private readonly collectionService = inject(CollectionTagService);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly libraryService = inject(LibraryService);
  private readonly uploadService = inject(UploadService);
  private readonly accountService = inject(AccountService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  tag = input.required<UserCollection>();

  series = signal<Array<Series>>([]);
  isLoading = signal(true);
  pagination = signal<Pagination>({totalPages: 1, totalItems: 200, itemsPerPage: 200, currentPage: 0});
  libraryNames = signal<{[key: number]: string}>({});
  selectedCover = signal('');
  coverImageDirty = signal(false);
  coverImageReset = signal(false);
  filterQuery = signal<string>('');
  chooserConfig = signal<CoverImageChooserConfig>({});
  activeTab = signal<Tabs>(Tabs.General);
  protected readonly seriesTracker = Tracker.IdTracker<Series>();

  private readonly formModel = signal<FormModel>({
    title: '',
    summary: '',
    coverImageLocked: false,
    promoted: false
  });

  formGroup = form(this.formModel, p => {
    required(p.title);

    disabled(p.title, () => this.tag().source !== ScrobbleProvider.Kavita);
    disabled(p.summary, () => this.tag().source !== ScrobbleProvider.Kavita);
    disabled(p.promoted, () => !this.accountService.hasPromoteRole());

    validateAsync(p.title, {
      params: (ctx) => {
        const name = ctx.value();
        if (name.trim().length === 0 || name === this.tag().title) return undefined;
        return name;
      },
      debounce: 100,
      factory: (params) => rxResource({
        params,
        stream: ({params: name}) => this.collectionService.tagNameExists(name)
      }),
      onSuccess: (exists) => exists ? {kind: 'duplicateName'} : null,
      onError: () => null
    });
  });

  protected readonly filteredSeries = computed(() =>
    this.series().filter(s => matchesQuery(s, this.filterQuery(), 'name', 'localizedName')));

  ngOnInit(): void {
    const tag = this.tag();
    this.formModel.set({
      title: tag.title,
      summary: tag.summary,
      coverImageLocked: tag.coverImageLocked,
      promoted: tag.promoted,
    });

    this.loadSeries();
  }

  onPageChange(pageNum: number) {
    this.pagination.update(p => ({...p, currentPage: pageNum}));
    this.loadSeries();
  }

  toggleAll() {
    this.seriesTracker.setAll(!this.seriesTracker.allSelected());
  }

  loadSeries() {
    forkJoin([
      this.seriesService.getSeriesForTag(this.tag().id, this.pagination().currentPage, this.pagination().itemsPerPage),
      this.libraryService.getLibraryNames()
    ]).subscribe(results => {
      const series = results[0];
      this.pagination.set(series.pagination);
      this.series.set(series.result);

      this.chooserConfig.set(this.coverChooserConfigFactory.forCollection(this.tag(), this.series()));

      this.seriesTracker.setData(this.series(), true);
      this.isLoading.set(false);

      this.libraryNames.set(results[1]);
    });
  }

  handleSelection(item: Series) {
    this.seriesTracker.toggle(item);
  }

  libraryName(libraryId: number) {
    return this.libraryNames()[libraryId];
  }

  close() {
    if (this.coverImageReset()) {
      this.modal.close(modalSaved(this.tag(), true));
    } else {
      this.modal.dismiss();
    }
  }

  async save() {
    const unselectedIds = this.seriesTracker.unselected().map(s => s.id);
    const tag: UserCollection = {...this.tag(), ...this.formModel()};

    if (unselectedIds.length == this.series().length &&
      !await this.confirmService.confirm(translate('toasts.no-series-collection-warning'))) {
      return;
    }

    let updatedTag: UserCollection | null = null;
    const apis: Observable<any>[] = [
      this.collectionService.updateTag(tag).pipe(tap(t => updatedTag = t)),
    ];

    if (unselectedIds.length > 0) {
      apis.push(this.collectionService.updateSeriesForTag(tag, unselectedIds));
    }

    if (this.coverImageDirty()) {
      apis.push(this.uploadService.updateCollectionCoverImage(this.tag().id, this.selectedCover()));
    }

    concat(...apis).pipe(
      delay(10),
      last()
    ).subscribe(() => {
      this.toastr.success(translate('toasts.collection-updated'));
      this.modal.close(modalSaved(updatedTag ?? tag, this.coverImageDirty()));
    });
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty.set(event.isDirty);
    this.selectedCover.set(event.fileName);
  }

  handleReset() {
    this.coverImageReset.set(true);
    this.formGroup.coverImageLocked().value.set(false);
    this.chooserConfig.set({ ...this.chooserConfig(), isLocked: false });
  }

  protected readonly Tabs = Tabs;
  protected readonly ScrobbleProvider = ScrobbleProvider;
}
