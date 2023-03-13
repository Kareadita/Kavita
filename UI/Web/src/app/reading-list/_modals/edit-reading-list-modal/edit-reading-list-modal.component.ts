import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { debounceTime, distinctUntilChanged, forkJoin, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { ReadingList } from 'src/app/_models/reading-list';
import { AccountService } from 'src/app/_services/account.service';
import { ImageService } from 'src/app/_services/image.service';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import { UploadService } from 'src/app/_services/upload.service';

enum TabID {
  General = 'General',
  CoverImage = 'Cover Image'
}

@Component({
  selector: 'app-edit-reading-list-modal',
  templateUrl: './edit-reading-list-modal.component.html',
  styleUrls: ['./edit-reading-list-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditReadingListModalComponent implements OnInit, OnDestroy {

  @Input() readingList!: ReadingList;
  reviewGroup!: FormGroup;

  coverImageIndex: number = 0;
   /**
    * Url of the selected cover
  */
  selectedCover: string = '';
  coverImageLocked: boolean = false;
  imageUrls: Array<string> = [];
  active = TabID.General;

  private readonly onDestroy = new Subject<void>();

  get Breakpoint() { return Breakpoint; }
  get TabID() { return TabID; }

  constructor(private ngModal: NgbActiveModal, private readingListService: ReadingListService, 
    public utilityService: UtilityService, private uploadService: UploadService, private toastr: ToastrService, 
    private imageService: ImageService, private readonly cdRef: ChangeDetectorRef, public accountService: AccountService) { }

  ngOnInit(): void {
    this.reviewGroup = new FormGroup({
      title: new FormControl(this.readingList.title, { nonNullable: true, validators: [Validators.required] }),
      summary: new FormControl(this.readingList.summary, { nonNullable: true, validators: [] }),
      promoted: new FormControl(this.readingList.promoted, { nonNullable: true, validators: [] }),
      startingMonth: new FormControl(this.readingList.startingMonth, { nonNullable: true, validators: [Validators.min(1), Validators.max(12)] }),
      startingYear: new FormControl(this.readingList.startingYear, { nonNullable: true, validators: [Validators.min(1000)] }),
      endingMonth: new FormControl(this.readingList.endingMonth, { nonNullable: true, validators: [Validators.min(1), Validators.max(12)] }),
      endingYear: new FormControl(this.readingList.endingYear, { nonNullable: true, validators: [Validators.min(1000)] }),
    });

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
      takeUntil(this.onDestroy)
      ).subscribe();

    this.imageUrls.push(this.imageService.randomize(this.imageService.getReadingListCoverImage(this.readingList.id)));
    if (!this.readingList.items || this.readingList.items.length === 0) {
      this.readingListService.getListItems(this.readingList.id).subscribe(items => {
        this.imageUrls.push(...(items).map(rli => this.imageService.getChapterCoverImage(rli.chapterId)));
      });
    } else {
      this.imageUrls.push(...(this.readingList.items).map(rli => this.imageService.getChapterCoverImage(rli.chapterId)));
    }
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  close() {
    this.ngModal.dismiss(undefined);
  }

  save() {
    if (this.reviewGroup.value.title.trim() === '') return;

    const model = {...this.reviewGroup.value, readingListId: this.readingList.id, coverImageLocked: this.coverImageLocked};
    model.startingMonth = model.startingMonth || 0;
    model.startingYear = model.startingYear || 0;
    model.endingMonth = model.endingMonth || 0;
    model.endingYear = model.endingYear || 0;
    const apis = [this.readingListService.update(model)];
    
    if (this.selectedCover !== '') {
      apis.push(this.uploadService.updateReadingListCoverImage(this.readingList.id, this.selectedCover))
    }
  
    forkJoin(apis).subscribe(results => {
      this.readingList.title = model.title;
      this.readingList.summary = model.summary;
      this.readingList.coverImageLocked = this.coverImageLocked;
      this.readingList.promoted = model.promoted;
      this.ngModal.close(this.readingList);
      this.toastr.success('Reading List updated');
    });
  }

  updateSelectedIndex(index: number) {
    this.coverImageIndex = index;
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
  }

  handleReset() {
    this.coverImageLocked = false;
  }

}
