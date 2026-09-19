import {
  ChangeDetectionStrategy,
  Component, computed,
  inject, input,
  OnInit,
  signal
} from '@angular/core';
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
import {concat, delay, last, Observable, tap} from 'rxjs';
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
import {TypeaheadConfig} from "../../../typeahead/_models/typeahead-config";
import {TypeaheadConfigFactoryService} from "../../../typeahead-config-factory.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {ReadingListService} from "../../../_services/reading-list.service";
import {UploadService} from "../../../_services/upload.service";
import {AccountService} from "../../../_services/account.service";
import {ReadingList} from "../../../_models/reading-list/reading-list";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, minLength, required} from "@angular/forms/signals";
import {uniqueName} from "../../../_validators/unique-name.validator";


@Component({
    selector: 'app-edit-reading-list-modal',
    templateUrl: './edit-reading-list-modal.component.html',
    styleUrls: ['./edit-reading-list-modal.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbNav, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavContent, NgbTooltip,
    NgTemplateOutlet, CoverImageChooserComponent, NgbNavOutlet, TranslocoDirective, TabTitlePipe, SettingItemComponent, TypeaheadComponent, FormFieldDirective, ValidationErrorsComponent, FormField]
})
export class EditReadingListModalComponent implements OnInit {

  private readonly ngModal = inject(NgbActiveModal);
  private readonly readingListService = inject(ReadingListService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly uploadService = inject(UploadService);
  private readonly toastr = inject(ToastrService);
  protected readonly accountService = inject(AccountService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadConfigFactoryService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  readingList = input.required<ReadingList>();
  originalReadingListTitle = computed(() => this.readingList().title);

  formModel = signal({
    title: '',
    summary: '',
    promoted: false,
    startingMonth: 0,
    startingYear: 0,
    endingMonth: 0,
    endingYear: 0,
    tags: [] as string[],
  });
  formGroup = form(this.formModel, path => {
    required(path.title);
    minLength(path.title, 1);
    uniqueName(path.title, 'readinglist', this.originalReadingListTitle);
  });

  selectedCover = signal('');
  coverImageDirty = signal(false);
  coverImageLocked = signal(false);
  coverImageReset = signal(false);
  tags = signal<ReadingListTag[]>([]);

  chooserConfig = signal<CoverImageChooserConfig>({});
  tagsSettings = signal<TypeaheadConfig<ReadingListTag> | null>(null);

  active = Tabs.General;

  protected readonly Tabs = Tabs;

  ngOnInit(): void {
    const readingList = this.readingList();

    this.formModel.set({
      title: readingList.title,
      summary: readingList.summary,
      promoted: readingList.promoted,
      startingMonth: readingList.startingMonth,
      startingYear: readingList.startingYear,
      endingMonth: readingList.endingMonth,
      endingYear: readingList.endingYear,
      tags: readingList.tags.map(t => t.title)
    });


    this.coverImageLocked.set(readingList.coverImageLocked);
    this.chooserConfig.set(this.coverChooserConfigFactory.forReadingList(this.readingList()));
    this.tagsSettings.set(this.typeaheadSettingsFactory.forReadingListTag({id: 'tags', savedData: this.readingList().tags ?? []}));
  }

  close() {
    if (this.coverImageReset()) {
      this.ngModal.close(modalSaved(this.readingList(), true));
    } else {
      this.ngModal.dismiss();
    }
  }

  save() {
    if (this.formGroup().invalid()) return;

    let updatedRL: ReadingList | null = null;

    const model = {...this.formModel(), readingListId: this.readingList().id, coverImageLocked: this.coverImageLocked()};

    const apis: Observable<unknown>[] = [this.readingListService.update(model).pipe(
      tap(result => updatedRL = result)
    )];

    if (this.coverImageDirty()) {
      apis.push(this.uploadService.updateReadingListCoverImage(this.readingList().id, this.selectedCover()));
    }

    concat(...apis).pipe(
      delay(10),
      last()
    ).subscribe(() => {
      this.ngModal.close(modalSaved(updatedRL, this.coverImageDirty()));
      this.toastr.success(translate('toasts.reading-list-updated'));
    });
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty.set(event.isDirty);
    this.selectedCover.set(event.fileName);
  }

  handleReset() {
    this.coverImageReset.set(true);
    this.coverImageLocked.set(false);
    this.chooserConfig.set({ ...this.chooserConfig(), isLocked: false });
  }

  updateTags(tags: ReadingListTag[]) {
    this.formGroup.tags().value.set(tags.map(t => t.title));
  }

}
