import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  OnInit
} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from '@angular/forms';
import {
  NgbActiveModal,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbPagination,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {concat, debounceTime, delay, distinctUntilChanged, forkJoin, last, Observable, switchMap, tap} from 'rxjs';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {UserCollection} from 'src/app/_models/collection-tag';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {SeriesService} from 'src/app/_services/series.service';
import {UploadService} from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DecimalPipe, NgTemplateOutlet} from "@angular/common";
import {CoverImageChooserComponent} from "../../cover-image-chooser/cover-image-chooser.component";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../../_services/cover-chooser-config-factory.service";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {AccountService} from "../../../_services/account.service";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";
import {SelectionModel} from "../../../typeahead/_models/selection-model";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";


@Component({
  selector: 'app-edit-collection-tags',
  imports: [NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, ReactiveFormsModule, FormsModule, NgbPagination,
    CoverImageChooserComponent, NgbNavOutlet, NgbTooltip, TranslocoDirective, NgTemplateOutlet, FilterPipe, DefaultDatePipe,
    SafeHtmlPipe, SafeUrlPipe, DecimalPipe, UtcToLocalTimePipe, TabTitlePipe],
  templateUrl: './edit-collection-tags-modal.component.html',
  styleUrls: ['./edit-collection-tags-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditCollectionTagsModalComponent implements OnInit {

  public readonly modal = inject(NgbActiveModal);
  public readonly utilityService = inject(UtilityService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly seriesService = inject(SeriesService);
  private readonly collectionService = inject(CollectionTagService);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly libraryService = inject(LibraryService);
  private readonly imageService = inject(ImageService);
  private readonly uploadService = inject(UploadService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly accountService = inject(AccountService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  tag = input.required<UserCollection>();

  series: Array<Series> = [];
  selections!: SelectionModel<Series>;
  isLoading: boolean = true;

  pagination!: Pagination;
  selectAll: boolean = true;
  libraryNames!: any;
  collectionTagForm!: FormGroup;
  active = Tabs.General;
  selectedCover: string = '';
  coverImageDirty = false;
  coverImageReset = false;
  chooserConfig: CoverImageChooserConfig = {};
  formGroup = new FormGroup({'filter': new FormControl('', [])});


  get hasSomeSelected() {
    return this.selections != null && this.selections.hasSomeSelected();
  }

  filterList = (listItem: Series) => {
    const query = (this.formGroup.get('filter')?.value || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(query) >= 0 || listItem.localizedName.toLowerCase().indexOf(query) >= 0;
  }

  constructor() {
    effect(() => {
      if (!this.accountService.hasPromoteRole()) {
        this.collectionTagForm.get('promoted')?.disable();
        this.cdRef.markForCheck();
      }
    });
  }


  ngOnInit(): void {
    if (this.pagination == undefined) {
      this.pagination = {totalPages: 1, totalItems: 200, itemsPerPage: 200, currentPage: 0};
    }
    const tag = this.tag();
    this.collectionTagForm = new FormGroup({
      title: new FormControl(tag.title, { nonNullable: true, validators: [Validators.required] }),
      summary: new FormControl(tag.summary, { nonNullable: true, validators: [] }),
      coverImageLocked: new FormControl(tag.coverImageLocked, { nonNullable: true, validators: [] }),
      promoted: new FormControl(tag.promoted, { nonNullable: true, validators: [] }),
    });

    if (tag.source !== ScrobbleProvider.Kavita) {
      this.collectionTagForm.get('title')?.disable();
      this.collectionTagForm.get('summary')?.disable();
    }


    this.collectionTagForm.get('title')?.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      switchMap(name => this.collectionService.tagNameExists(name)),
      tap(exists => {
        const isExistingName = this.collectionTagForm.get('title')?.value === this.tag().title;
        if (!exists || isExistingName) {
          this.collectionTagForm.get('title')?.setErrors(null);
        } else {
          this.collectionTagForm.get('title')?.setErrors({duplicateName: true})
        }
        this.cdRef.markForCheck();
      }),
      takeUntilDestroyed(this.destroyRef)
      ).subscribe();

    this.loadSeries();
  }

  onPageChange(pageNum: number) {
    this.pagination.currentPage = pageNum;
    this.loadSeries();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.series.forEach(s => this.selections.toggle(s, this.selectAll));
    this.cdRef.markForCheck();
  }

  loadSeries() {
    forkJoin([
      this.seriesService.getSeriesForTag(this.tag().id, this.pagination.currentPage, this.pagination.itemsPerPage),
      this.libraryService.getLibraryNames()
    ]).subscribe(results => {
      const series = results[0];
      this.pagination = series.pagination;
      this.series = series.result;

      this.chooserConfig = this.coverChooserConfigFactory.forCollection(this.tag(), this.series);

      this.selections = new SelectionModel<Series>(true, this.series);
      this.isLoading = false;

      this.libraryNames = results[1];
      this.cdRef.markForCheck();
    });
  }

  handleSelection(item: Series) {
    this.selections.toggle(item);
    const numberOfSelected = this.selections.selected().length;
    if (numberOfSelected == 0) {
      this.selectAll = false;
    } else if (numberOfSelected == this.series.length) {
      this.selectAll = true;
    }
    this.cdRef.markForCheck();
  }

  libraryName(libraryId: number) {
    return this.libraryNames[libraryId];
  }

  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.tag(), true));
    } else {
      this.modal.dismiss();
    }
  }

  async save() {
    const unselectedIds = this.selections.unselected().map(s => s.id);
    const tag = this.collectionTagForm.value;

    tag.id = this.tag().id;
    tag.title = this.collectionTagForm.get('title')!.value;
    tag.summary = this.collectionTagForm.get('summary')!.value;


    if (unselectedIds.length == this.series.length &&
      !await this.confirmService.confirm(translate('toasts.no-series-collection-warning'))) {
      return;
    }

    let updatedTag: UserCollection | null = null;
    const apis: Observable<any>[] = [
      this.collectionService.updateTag(tag).pipe(tap(t => updatedTag = t)),
    ];

    const unselectedSeries = this.selections.unselected().map(s => s.id);
    if (unselectedSeries.length > 0) {
      apis.push(this.collectionService.updateSeriesForTag(tag, unselectedSeries));
    }

    if (this.coverImageDirty) {
      apis.push(this.uploadService.updateCollectionCoverImage(this.tag().id, this.selectedCover));
    }

    concat(...apis).pipe(
      delay(10),
      last()
    ).subscribe(() => {
      this.toastr.success(translate('toasts.collection-updated'));
      this.modal.close(modalSaved(updatedTag ?? tag, this.coverImageDirty));
    });
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.collectionTagForm.patchValue({ coverImageLocked: false });
    this.chooserConfig = { ...this.chooserConfig, isLocked: false };
    this.cdRef.markForCheck();
  }

  protected readonly Tabs = Tabs;
  protected readonly ScrobbleProvider = ScrobbleProvider;
}
