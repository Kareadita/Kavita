import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {FormBuilder, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {AsyncPipe} from "@angular/common";
import {NgbActiveModal, NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../_services/account.service";
import {Chapter} from "../../_models/chapter";
import {LibraryType} from "../../_models/library/library";
import {TypeaheadSettings} from "../../typeahead/_models/typeahead-settings";
import {Tag} from "../../_models/tag";
import {Language} from "../../_models/metadata/language";
import {Person} from "../../_models/metadata/person";
import {Genre} from "../../_models/metadata/genre";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {PublicationStatusDto} from "../../_models/metadata/publication-status-dto";
import {SeriesService} from "../../_services/series.service";
import {ImageService} from "../../_services/image.service";
import {LibraryService} from "../../_services/library.service";
import {UploadService} from "../../_services/upload.service";
import {MetadataService} from "../../_services/metadata.service";
import {ToastrService} from "ngx-toastr";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ActionService} from "../../_services/action.service";
import {DownloadService} from "../../shared/_services/download.service";
import {Series} from "../../_models/series";

enum TabID {
  General = 'general-tab',
  CoverImage = 'cover-image-tab',
  Info = 'info-tab',
  Tasks = 'tasks-tab'
}

const blackList: Array<Action> = [];

@Component({
  selector: 'app-edit-chapter-modal',
  standalone: true,
  imports: [
    FormsModule,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    TranslocoDirective,
    AsyncPipe,
    NgbNavOutlet,
    ReactiveFormsModule,
    NgbNavItem
  ],
  templateUrl: './edit-chapter-modal.component.html',
  styleUrl: './edit-chapter-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditChapterModalComponent implements OnInit {

  public readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
  public readonly utilityService = inject(UtilityService);
  private readonly fb = inject(FormBuilder);
  public readonly imageService = inject(ImageService);
  private readonly libraryService = inject(LibraryService);
  private readonly uploadService = inject(UploadService);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly downloadService = inject(DownloadService);

  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;

  @Input({required: true}) chapter!: Chapter;
  @Input({required: true}) libraryType!: LibraryType;

  activeId = TabID.General;
  editForm: FormGroup = new FormGroup({});
  selectedCover: string = '';
  coverImageReset = false;

  tagsSettings: TypeaheadSettings<Tag> = new TypeaheadSettings();
  languageSettings: TypeaheadSettings<Language> = new TypeaheadSettings();
  peopleSettings: {[PersonRole: string]: TypeaheadSettings<Person>} = {};
  genreSettings: TypeaheadSettings<Genre> = new TypeaheadSettings();

  tags: Tag[] = [];
  genres: Genre[] = [];
  ageRatings: Array<AgeRatingDto> = [];
  publicationStatuses: Array<PublicationStatusDto> = [];
  validLanguages: Array<Language> = [];

  tasks = this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getChapterActions(this.runTask.bind(this)), blackList);



  ngOnInit() {
    //....
  }

  close() {

  }

  save() {

  }

  unlock(b: any, field: string) {
    if (b) {
      b[field] = !b[field];
    }
    this.cdRef.markForCheck();
  }

  async runTask(action: ActionItem<Chapter>) {
    switch (action.action) {

      case Action.MarkAsRead:
        //this.actionService.markChapterAsRead(this.li);
        break;
      case Action.MarkAsUnread:
        //this.actionService.markChapterAsUnread(this.chapter);
        break;
      case Action.Delete:
        //await this.actionService.deleteSeries(this.series);
        break;
      case Action.Download:
        this.downloadService.download('chapter', this.chapter);
        break;
    }
  }

}
