import {AfterViewInit, ChangeDetectorRef, Component, ElementRef, inject, Input, OnInit, ViewChild} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from "ngx-toastr";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ReadingProfileService} from "../../../_services/reading-profile.service";
import {ReadingProfile} from "../../../_models/preferences/reading-profiles";
import {FilterPipe} from "../../../_pipes/filter.pipe";

@Component({
  selector: 'app-bulk-set-reading-profile',
  imports: [
    ReactiveFormsModule,
    FilterPipe,
    TranslocoDirective
  ],
  templateUrl: './bulk-set-reading-profile.component.html',
  styleUrl: './bulk-set-reading-profile.component.scss'
})
export class BulkSetReadingProfileComponent implements OnInit, AfterViewInit {
  private readonly modal = inject(NgbActiveModal);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly MaxItems = 8;

  @Input({required: true}) title!: string;
  /**
   * Series Ids to add to Reading Profile
   */
  @Input() seriesIds: Array<number> = [];
  @Input() libraryId: number | undefined;
  @ViewChild('title') inputElem!: ElementRef<HTMLInputElement>;

  profiles: Array<ReadingProfile> = [];
  loading: boolean = false;
  profileForm: FormGroup = new FormGroup({});

  ngOnInit(): void {

    this.profileForm.addControl('title', new FormControl(this.title, []));
    this.profileForm.addControl('filterQuery', new FormControl('', []));

    this.loading = true;
    this.cdRef.markForCheck();
    this.readingProfileService.all().subscribe(profiles => {
      this.profiles = profiles;
      this.loading = false;
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
}
