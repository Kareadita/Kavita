import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {
  NgbActiveModal,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavItemRole,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {concat, debounceTime, delay, distinctUntilChanged, last, Observable, of, switchMap, tap} from 'rxjs';
import {ReadingList} from 'src/app/_models/reading-list/reading-list';
import {AccountService} from 'src/app/_services/account.service';
import {ImageService} from 'src/app/_services/image.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {UploadService} from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CoverImageChooserComponent} from '../../../cards/cover-image-chooser/cover-image-chooser.component';
import {NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";
import {ReadingListTag} from "../../../_models/reading-list/reading-list-tag";
import {TypeaheadSettings} from "../../../typeahead/_models/typeahead-settings";
import {Tag} from "../../../_models/tag";
import {map} from "rxjs/operators";
import {UtilityService} from "../../../shared/_services/utility.service";
import {MetadataService} from "../../../_services/metadata.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";


@Component({
    selector: 'app-edit-reading-list-modal',
    templateUrl: './edit-reading-list-modal.component.html',
    styleUrls: ['./edit-reading-list-modal.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbNav, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavContent, ReactiveFormsModule, NgbTooltip,
    NgTemplateOutlet, CoverImageChooserComponent, NgbNavOutlet, TranslocoDirective, TabTitlePipe, SettingItemComponent, TypeaheadComponent]
})
export class EditReadingListModalComponent implements OnInit {
  private readonly ngModal = inject(NgbActiveModal);
  private readonly readingListService = inject(ReadingListService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly uploadService = inject(UploadService);
  private readonly toastr = inject(ToastrService);
  private readonly imageService = inject(ImageService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly utilityService = inject(UtilityService);
  private readonly metadataService = inject(MetadataService);


  @Input({required: true}) readingList!: ReadingList;

  reviewGroup!: FormGroup;
  coverImageIndex: number = 0;
   /**
    * Url of the selected cover
  */
  selectedCover: string = '';
  coverImageLocked: boolean = false;
  imageUrls: Array<string> = [];
  active = Tabs.General;
  tags: ReadingListTag[] = [];
  tagsSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();

  protected readonly Tabs = Tabs;

  ngOnInit(): void {
    this.reviewGroup = new FormGroup({
      title: new FormControl(this.readingList.title, { nonNullable: true, validators: [Validators.required] }),
      summary: new FormControl(this.readingList.summary, { nonNullable: true, validators: [] }),
      promoted: new FormControl(this.readingList.promoted, { nonNullable: true, validators: [] }),
      startingMonth: new FormControl(this.readingList.startingMonth, { nonNullable: true, validators: [Validators.min(1), Validators.max(12)] }),
      startingYear: new FormControl(this.readingList.startingYear, { nonNullable: true, validators: [Validators.min(1000)] }),
      endingMonth: new FormControl(this.readingList.endingMonth, { nonNullable: true, validators: [Validators.min(1), Validators.max(12)] }),
      endingYear: new FormControl(this.readingList.endingYear, { nonNullable: true, validators: [Validators.min(1000)] }),
      tags: new FormControl(this.readingList.tags, { nonNullable: true, validators: [] })
    });

    this.coverImageLocked = this.readingList.coverImageLocked;
    this.tags = this.readingList.tags;

    this.reviewGroup.get('title')?.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      switchMap(name => this.readingListService.nameExists(name)),
      tap(exists => {
        const isExistingName = this.reviewGroup.get('title')?.value === this.readingList.title;
        if (!exists || isExistingName) {
          this.reviewGroup.get('title')?.setErrors(null);
        } else {
          this.reviewGroup.get('title')?.setErrors({duplicateName: true})
        }
        this.cdRef.markForCheck();
      }),
      takeUntilDestroyed(this.destroyRef)
      ).subscribe();

    this.imageUrls.push(this.imageService.randomize(this.imageService.getReadingListCoverImage(this.readingList.id)));
    if (!this.readingList.items || this.readingList.items.length === 0) {
      this.readingListService.getListItems(this.readingList.id).subscribe(items => {
        this.imageUrls.push(...(items).map(rli => this.imageService.getChapterCoverImage(rli.chapterId)));
      });
    } else {
      this.imageUrls.push(...(this.readingList.items).map(rli => this.imageService.getChapterCoverImage(rli.chapterId)));
    }

    this.setupTagSettings();
  }

  close() {
    this.ngModal.dismiss();
  }

  setupTagSettings() {
    this.tagsSettings.minCharacters = 0;
    this.tagsSettings.multiple = true;
    this.tagsSettings.id = 'tags';
    this.tagsSettings.unique = true;
    this.tagsSettings.showLocked = true;
    this.tagsSettings.addIfNonExisting = true;


    this.tagsSettings.compareFn = (options: Tag[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.title, filter));
    }
    this.tagsSettings.fetchFn = (filter: string) => this.metadataService.getAllReadingListTags()
      .pipe(map(items => this.tagsSettings.compareFn(items, filter)));

    this.tagsSettings.addTransformFn = ((title: string) => {
      return {id: 0, title: title };
    });
    this.tagsSettings.selectionCompareFn = (a: Tag, b: Tag) => {
      return a.title.toLowerCase() == b.title.toLowerCase();
    }
    this.tagsSettings.compareFnForAdd = (options: Tag[], filter: string) => {
      return options.filter(m => this.utilityService.filterMatches(m.title, filter));
    }
    this.tagsSettings.trackByIdentityFn = (index, value) => value.title + (value.id + '');

    if (this.readingList.tags) {
      this.tagsSettings.savedData = this.readingList.tags;
    }
    return of(true);
  }

  save() {
    if (this.reviewGroup.value.title.trim() === '') return;

    let updatedRL: ReadingList | null = null;

    const model = {...this.reviewGroup.value, readingListId: this.readingList.id, coverImageLocked: this.coverImageLocked};
    model.startingMonth = model.startingMonth || 0;
    model.startingYear = model.startingYear || 0;
    model.endingMonth = model.endingMonth || 0;
    model.endingYear = model.endingYear || 0;
    model.tags = this.tags.map(t => t.title);

    const apis: Observable<any>[] = [this.readingListService.update(model).pipe(
      tap(result => updatedRL = result)
    )];

    if (this.selectedCover !== '') {
      apis.push(this.uploadService.updateReadingListCoverImage(this.readingList.id, this.selectedCover));
    }

    concat(...apis).pipe(
      delay(10),
      last()
    ).subscribe(() => {
      this.ngModal.close(modalSaved(updatedRL, this.selectedCover !== ''));
      this.toastr.success(translate('toasts.reading-list-updated'));
    });
  }

  updateSelectedIndex(index: number) {
    this.coverImageIndex = index;
    this.cdRef.markForCheck();
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageLocked = false;
    this.cdRef.markForCheck();
  }

  updateTags(tags: ReadingListTag[]) {
    this.tags = tags;
    this.readingList.tags = tags;
  }

}
