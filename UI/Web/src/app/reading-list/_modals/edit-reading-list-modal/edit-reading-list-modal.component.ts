import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnInit,
  signal
} from '@angular/core';
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
import {ToastrService} from '@openng/ngx-toastr';
import {concat, debounceTime, delay, distinctUntilChanged, last, Observable, switchMap, tap} from 'rxjs';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CoverImageChooserComponent} from '../../../cards/cover-image-chooser/cover-image-chooser.component';
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from '../../../_services/cover-chooser-config-factory.service';
import {NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";
import {ReadingListTag} from "../../../_models/reading-list/reading-list-tag";
import {TypeaheadSettings} from "../../../typeahead/_models/typeahead-settings";
import {Tag} from "../../../_models/tag";
import {TypeaheadSettingsFactoryService} from "../../../typeahead-settings-factory.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {ReadingListService} from "../../../_services/reading-list.service";
import {UploadService} from "../../../_services/upload.service";
import {AccountService} from "../../../_services/account.service";
import {ReadingList} from "../../../_models/reading-list/reading-list";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";


@Component({
    selector: 'app-edit-reading-list-modal',
    templateUrl: './edit-reading-list-modal.component.html',
    styleUrls: ['./edit-reading-list-modal.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbNav, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavContent, ReactiveFormsModule, NgbTooltip,
    NgTemplateOutlet, CoverImageChooserComponent, NgbNavOutlet, TranslocoDirective, TabTitlePipe, SettingItemComponent, TypeaheadComponent, FormFieldDirective, ValidationErrorsComponent]
})
export class EditReadingListModalComponent implements OnInit {
  private readonly ngModal = inject(NgbActiveModal);
  private readonly readingListService = inject(ReadingListService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly uploadService = inject(UploadService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly typeaheadSettingsFactory = inject(TypeaheadSettingsFactoryService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  @Input({required: true}) readingList!: ReadingList;

  reviewGroup!: FormGroup;
  selectedCover: string = '';
  coverImageDirty = false;
  coverImageLocked: boolean = false;
  coverImageReset = false;
  chooserConfig = signal<CoverImageChooserConfig>({});
  active = Tabs.General;
  tags: ReadingListTag[] = [];
  tagsSettings = signal<TypeaheadSettings<Tag> | null>(null);

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
    this.chooserConfig.set(this.coverChooserConfigFactory.forReadingList(this.readingList));

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

    this.tagsSettings.set(this.typeaheadSettingsFactory.forTag({id: 'tags', source: 'readingList',
      savedData: this.readingList.tags ?? []}));
  }

  close() {
    if (this.coverImageReset) {
      this.ngModal.close(modalSaved(this.readingList, true));
    } else {
      this.ngModal.dismiss();
    }
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

    if (this.coverImageDirty) {
      apis.push(this.uploadService.updateReadingListCoverImage(this.readingList.id, this.selectedCover));
    }

    concat(...apis).pipe(
      delay(10),
      last()
    ).subscribe(() => {
      this.ngModal.close(modalSaved(updatedRL, this.coverImageDirty));
      this.toastr.success(translate('toasts.reading-list-updated'));
    });
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.coverImageLocked = false;
    this.chooserConfig.set({ ...this.chooserConfig(), isLocked: false });
  }

  updateTags(tags: ReadingListTag[]) {
    this.tags = tags;
    this.readingList.tags = tags;
  }

}
