import {AfterViewInit, ChangeDetectorRef, Component, ElementRef, inject, Input, OnInit, ViewChild} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from "ngx-toastr";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ReadingProfileService} from "../../../_services/reading-profile.service";
import {ReadingProfile, ReadingProfileKind} from "../../../_models/preferences/reading-profiles";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";

@Component({
  selector: 'app-bulk-set-reading-profile-modal',
  imports: [
    ReactiveFormsModule,
    FilterPipe,
    TranslocoDirective,
    SentenceCasePipe
  ],
  templateUrl: './bulk-set-reading-profile-modal.component.html',
  styleUrl: './bulk-set-reading-profile-modal.component.scss'
})
export class BulkSetReadingProfileModalComponent implements OnInit, AfterViewInit {
  private readonly modal = inject(NgbActiveModal);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly MaxItems = 8;

  /**
   * Modal Header - since this code is used for multiple flows
   */
  @Input({required: true}) title!: string;
  /**
   * Series Ids to add to Reading Profile
   */
  @Input() seriesIds: Array<number> = [];
  @Input() libraryId: number | undefined;
  @ViewChild('title') inputElem!: ElementRef<HTMLInputElement>;

  currentProfile: ReadingProfile | null = null;
  profiles: Array<ReadingProfile> = [];
  isLoading: boolean = false;
  profileForm: FormGroup = new FormGroup({
    filterQuery: new FormControl('', []), // Used for inline filtering when too many RPs
  });

  ngOnInit(): void {

    this.profileForm.addControl('title', new FormControl(this.title, []));

    this.isLoading = true;
    this.cdRef.markForCheck();

    if (this.libraryId !== undefined) {
      this.readingProfileService.getForLibrary(this.libraryId).subscribe(profile => {
        this.currentProfile = profile;
        this.cdRef.markForCheck();
      });
    } else if (this.seriesIds.length === 1) {
      this.readingProfileService.getForSeries(this.seriesIds[0], true).subscribe(profile => {
        this.currentProfile = profile;
        this.cdRef.markForCheck();
      });
    }


    this.readingProfileService.getAllProfiles().subscribe(profiles => {
      this.profiles = profiles;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  ngAfterViewInit() {
    // Shift focus to input
    if (this.inputElem) {
      this.inputElem.nativeElement.select();
      this.cdRef.markForCheck();
    }
  }

  close() {
    this.modal.close();
  }

  addToProfile(profile: ReadingProfile) {
    if (this.seriesIds.length == 1) {
      this.readingProfileService.addToSeries(profile.id, this.seriesIds[0]).subscribe(() => {
        this.toastr.success(translate('toasts.series-bound-to-reading-profile', {name: profile.name}));
        this.modal.close();
      });
      return;
    }

    if (this.seriesIds.length > 1) {
      this.readingProfileService.bulkAddToSeries(profile.id, this.seriesIds).subscribe(() => {
        this.toastr.success(translate('toasts.series-bound-to-reading-profile', {name: profile.name}));
        this.modal.close();
      });
      return;
    }

    if (this.libraryId) {
      this.readingProfileService.addToLibrary(profile.id, this.libraryId).subscribe(() => {
        this.toastr.success(translate('toasts.library-bound-to-reading-profile', {name: profile.name}));
        this.modal.close();
      });
    }
  }

  filterList = (listItem: ReadingProfile) => {
    return listItem.name.toLowerCase().indexOf((this.profileForm.value.filterQuery || '').toLowerCase()) >= 0;
  }

  clear() {
    this.profileForm.get('filterQuery')?.setValue('');
  }

  protected readonly ReadingProfileKind = ReadingProfileKind;
}
