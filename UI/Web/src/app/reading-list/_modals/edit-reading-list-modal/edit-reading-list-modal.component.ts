import { Component, Input, OnInit } from '@angular/core';
import { UntypedFormGroup, UntypedFormControl, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { forkJoin } from 'rxjs';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { ReadingList } from 'src/app/_models/reading-list';
import { ImageService } from 'src/app/_services/image.service';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import { UploadService } from 'src/app/_services/upload.service';


@Component({
  selector: 'app-edit-reading-list-modal',
  templateUrl: './edit-reading-list-modal.component.html',
  styleUrls: ['./edit-reading-list-modal.component.scss']
})
export class EditReadingListModalComponent implements OnInit {

  @Input() readingList!: ReadingList;
  reviewGroup!: UntypedFormGroup;

  coverImageIndex: number = 0;
   /**
    * Url of the selected cover
  */
  selectedCover: string = '';
  coverImageLocked: boolean = false;

  imageUrls: Array<string> = [];

  tabs = [{title: 'General', disabled: false}, {title: 'Cover', disabled: false}];
  active = this.tabs[0];

  get Breakpoint() {
    return Breakpoint;
  }

  constructor(private ngModal: NgbActiveModal, private readingListService: ReadingListService, 
    public utilityService: UtilityService, private uploadService: UploadService, private toastr: ToastrService, 
    private imageService: ImageService) { }

  ngOnInit(): void {
    this.reviewGroup = new UntypedFormGroup({
      title: new UntypedFormControl(this.readingList.title, [Validators.required]),
      summary: new UntypedFormControl(this.readingList.summary, [])
    });

    this.imageUrls.push(this.imageService.randomize(this.imageService.getReadingListCoverImage(this.readingList.id)));
  }

  close() {
    this.ngModal.dismiss(undefined);
  }

  save() {
    if (this.reviewGroup.value.title.trim() === '') return;


    const model = {...this.reviewGroup.value, readingListId: this.readingList.id, promoted: this.readingList.promoted, coverImageLocked: this.coverImageLocked};

    const apis = [this.readingListService.update(model)];
    
    if (this.selectedCover !== '') {
      apis.push(this.uploadService.updateReadingListCoverImage(this.readingList.id, this.selectedCover))
    }
  
    forkJoin(apis).subscribe(results => {
      this.readingList.title = model.title;
      this.readingList.summary = model.summary;
      this.readingList.coverImageLocked = this.coverImageLocked;
      this.ngModal.close(this.readingList);
      this.toastr.success('Reading List updated');
    });
  }

  togglePromotion() {
    const originalPromotion = this.readingList.promoted;
    this.readingList.promoted = !this.readingList.promoted;
    const model = {readingListId: this.readingList.id, promoted: this.readingList.promoted};
    this.readingListService.update(model).subscribe(res => {
      /* No Operation */
    }, err => {
      this.readingList.promoted = originalPromotion;
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
