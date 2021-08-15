import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Chapter } from 'src/app/_models/chapter';
import { ImageService } from 'src/app/_services/image.service';
import { UploadService } from 'src/app/_services/upload.service';

@Component({
  selector: 'app-change-cover-image-modal',
  templateUrl: './change-cover-image-modal.component.html',
  styleUrls: ['./change-cover-image-modal.component.scss']
})
export class ChangeCoverImageModalComponent implements OnInit {

  @Input() chapter!: Chapter;
  @Input() title: string = '';

  selectedCover: string = '';
  imageUrls: Array<string> = [];
  coverImageIndex: number = 0;
  coverImageLocked: boolean = false;
  loading: boolean = false;

  constructor(private imageService: ImageService, private uploadService: UploadService, public modal: NgbActiveModal) { }

  ngOnInit(): void {
    // Randomization isn't needed as this is only the chooser
    this.imageUrls.push(this.imageService.getChapterCoverImage(this.chapter.id));
  }

  cancel() {
    this.modal.close({success: false, coverImageUpdate: false})
  }
  save() {
    this.loading = true;
    if (this.coverImageIndex > 0) {
      this.chapter.coverImageLocked = true;
      this.uploadService.updateChapterCoverImage(this.chapter.id, this.selectedCover).subscribe(() => {
        if (this.coverImageIndex > 0) {
          this.chapter.coverImageLocked = true;
        }
        this.modal.close({success: true, chapter: this.chapter, coverImageUpdate: this.chapter.coverImageLocked});
        this.loading = false;
      }, err => this.loading = false);
    } else {
      this.modal.close({success: true, chapter: this.chapter, coverImageUpdate: this.chapter.coverImageLocked});
    }

    
  }

  updateSelectedIndex(index: number) {
    this.coverImageIndex = index;
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
  }

  handleReset() {
    this.coverImageLocked = false;
    this.chapter.coverImageLocked = false;
  }

}
