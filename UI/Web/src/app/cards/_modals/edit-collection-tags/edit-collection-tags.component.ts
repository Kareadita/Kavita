import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
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
import {debounceTime, distinctUntilChanged, forkJoin, switchMap, tap} from 'rxjs';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {UserCollection} from 'src/app/_models/collection-tag';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {SeriesService} from 'src/app/_services/series.service';
import {UploadService} from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DatePipe, DecimalPipe, NgIf, NgTemplateOutlet} from "@angular/common";
import {CoverImageChooserComponent} from "../../cover-image-chooser/cover-image-chooser.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {AccountService} from "../../../_services/account.service";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";
import {SelectionModel} from "../../../typeahead/_models/selection-model";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";


enum TabID {
  General = 'general-tab',
  CoverImage = 'cover-image-tab',
  Series = 'series-tab',
  Info = 'info-tab'
}

@Component({
  selector: 'app-edit-collection-tags',
  standalone: true,
  imports: [NgbNav, NgbNavItem, NgbNavLink, NgbNavContent, ReactiveFormsModule, FormsModule, NgbPagination,
    CoverImageChooserComponent, NgbNavOutlet, NgbTooltip, TranslocoDirective, NgTemplateOutlet, FilterPipe, DatePipe, DefaultDatePipe, ReadMoreComponent, SafeHtmlPipe, SafeUrlPipe, MangaFormatPipe, NgIf, SentenceCasePipe, TagBadgeComponent, DecimalPipe, UtcToLocalTimePipe],
  templateUrl: './edit-collection-tags.component.html',
  styleUrls: ['./edit-collection-tags.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditCollectionTagsComponent implements OnInit {

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

  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;
  protected readonly ScrobbleProvider = ScrobbleProvider;

  @Input({required: true}) tag!: UserCollection;

  series: Array<Series> = [];
  selections!: SelectionModel<Series>;
  isLoading: boolean = true;

  pagination!: Pagination;
  selectAll: boolean = true;
  libraryNames!: any;
  collectionTagForm!: FormGroup;
  active = TabID.General;
  imageUrls: Array<string> = [];
  selectedCover: string = '';
  formGroup = new FormGroup({'filter': new FormControl('', [])});


  get hasSomeSelected() {
    return this.selections != null && this.selections.hasSomeSelected();
  }

  filterList = (listItem: Series) => {
    const query = (this.formGroup.get('filter')?.value || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(query) >= 0 || listItem.localizedName.toLowerCase().indexOf(query) >= 0;
  }


  ngOnInit(): void {
    if (this.pagination == undefined) {
      this.pagination = {totalPages: 1, totalItems: 200, itemsPerPage: 200, currentPage: 0};
    }
    this.collectionTagForm = new FormGroup({
      title: new FormControl(this.tag.title, { nonNullable: true, validators: [Validators.required] }),
      summary: new FormControl(this.tag.summary, { nonNullable: true, validators: [] }),
      coverImageLocked: new FormControl(this.tag.coverImageLocked, { nonNullable: true, validators: [] }),
      coverImageIndex: new FormControl(0, { nonNullable: true, validators: [] }),
      promoted: new FormControl(this.tag.promoted, { nonNullable: true, validators: [] }),
    });

    if (this.tag.source !== ScrobbleProvider.Kavita) {
      this.collectionTagForm.get('title')?.disable();
      this.collectionTagForm.get('summary')?.disable();
    }

    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      if (!user) return;
      if (!this.accountService.hasPromoteRole(user)) {
        this.collectionTagForm.get('promoted')?.disable();
        this.cdRef.markForCheck();
      }
    });


    this.collectionTagForm.get('title')?.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      switchMap(name => this.collectionService.tagNameExists(name)),
      tap(exists => {
        const isExistingName = this.collectionTagForm.get('title')?.value === this.tag.title;
        if (!exists || isExistingName) {
          this.collectionTagForm.get('title')?.setErrors(null);
        } else {
          this.collectionTagForm.get('title')?.setErrors({duplicateName: true})
        }
        this.cdRef.markForCheck();
      }),
      takeUntilDestroyed(this.destroyRef)
      ).subscribe();

    this.imageUrls.push(this.imageService.randomize(this.imageService.getCollectionCoverImage(this.tag.id)));
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
      this.seriesService.getSeriesForTag(this.tag.id, this.pagination.currentPage, this.pagination.itemsPerPage),
      this.libraryService.getLibraryNames()
    ]).subscribe(results => {
      const series = results[0];
      this.pagination = series.pagination;
      this.series = series.result;

      this.imageUrls.push(...this.series.map(s => this.imageService.getSeriesCoverImage(s.id)));
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
    this.modal.dismiss();
  }

  async save() {
    const selectedIndex = this.collectionTagForm.get('coverImageIndex')?.value || 0;
    const unselectedIds = this.selections.unselected().map(s => s.id);
    const tag = this.collectionTagForm.value;
    tag.id = this.tag.id;
    tag.title = this.collectionTagForm.get('title')!.value;
    tag.summary = this.collectionTagForm.get('summary')!.value;


    if (unselectedIds.length == this.series.length &&
      !await this.confirmService.confirm(translate('toasts.no-series-collection-warning'))) {
      return;
    }

    const apis = [
      this.collectionService.updateTag(tag),
    ];

    const unselectedSeries = this.selections.unselected().map(s => s.id);
    if (unselectedSeries.length > 0) {
      apis.push(this.collectionService.updateSeriesForTag(tag, unselectedSeries));
    }

    if (selectedIndex > 0) {
      apis.push(this.uploadService.updateCollectionCoverImage(this.tag.id, this.selectedCover));
    }

    forkJoin(apis).subscribe(() => {
      this.modal.close({success: true, coverImageUpdated: selectedIndex > 0});
      this.toastr.success(translate('toasts.collection-updated'));
    });
  }

  updateSelectedIndex(index: number) {
    this.collectionTagForm.patchValue({
      coverImageIndex: index
    });
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.collectionTagForm.patchValue({
      coverImageLocked: false
    });
    this.cdRef.markForCheck();
  }
}
