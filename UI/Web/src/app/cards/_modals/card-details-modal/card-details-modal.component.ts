import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Volume } from 'src/app/_models/volume';
import { ImageService } from 'src/app/_services/image.service';
import { ChangeCoverImageModalComponent } from '../change-cover-image/change-cover-image-modal.component';



@Component({
  selector: 'app-card-details-modal',
  templateUrl: './card-details-modal.component.html',
  styleUrls: ['./card-details-modal.component.scss']
})
export class CardDetailsModalComponent implements OnInit {

  @Input() parentName = '';
  @Input() seriesId: number = 0;
  @Input() data!: any; // Volume | Chapter
  isChapter = false;
  chapters: Chapter[] = [];
  seriesVolumes: any[] = [];
  isLoadingVolumes = false;
  formatKeys = Object.keys(MangaFormat);
  /**
   * If a cover image update occured. 
   */
  coverImageUpdate: boolean = false; 


  constructor(private modalService: NgbModal, public modal: NgbActiveModal, public utilityService: UtilityService, 
    public imageService: ImageService) { }

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.data);

    if (this.isChapter) {
      this.chapters.push(this.data);
    } else if (!this.isChapter) {
      this.chapters.push(...this.data?.chapters);
    }
    this.chapters.sort(this.utilityService.sortChapters);
    // Try to show an approximation of the reading order for files
    var collator = new Intl.Collator(undefined, {numeric: true, sensitivity: 'base'});
    this.chapters.forEach((c: Chapter) => {
      c.files.sort((a: MangaFile, b: MangaFile) => collator.compare(a.filePath, b.filePath));
    });
  }

  close() {
    this.modal.close({coverImageUpdate: this.coverImageUpdate});
  }

  formatChapterNumber(chapter: Chapter) {
    if (chapter.number === '0') {
      return '1';
    }
    return chapter.number;
  }

  updateCover() {
    const modalRef = this.modalService.open(ChangeCoverImageModalComponent, {  size: 'lg' }); // scrollable: true, size: 'lg', windowClass: 'scrollable-modal' (these don't work well on mobile)
    if (this.utilityService.isChapter(this.data)) {
      const chapter = this.utilityService.asChapter(this.data)
      modalRef.componentInstance.chapter = chapter;
      modalRef.componentInstance.title = 'Select ' + (chapter.isSpecial ? '' : 'Chapter ') + chapter.range + '\'s Cover';
    } else {
      const volume = this.utilityService.asVolume(this.data);
      const chapters = volume.chapters;
      if (chapters && chapters.length > 0) {
        modalRef.componentInstance.chapter = chapters[0];
        modalRef.componentInstance.title = 'Select Volume ' + volume.number + '\'s Cover';
      }
    }
    
    modalRef.closed.subscribe((closeResult: {success: boolean, coverImageUpdate: boolean}) => {
      if (closeResult.success) {
        this.coverImageUpdate = closeResult.coverImageUpdate;
      }
    });
  }
}
