import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Volume } from 'src/app/_models/volume';
import { ImageService } from 'src/app/_services/image.service';
import { NaturalSortService } from '../../_services/natural-sort.service';
import { UtilityService } from '../../_services/utility.service';



@Component({
  selector: 'app-card-details-modal',
  templateUrl: './card-details-modal.component.html',
  styleUrls: ['./card-details-modal.component.scss']
})
export class CardDetailsModalComponent implements OnInit {

  @Input() parentName = '';
  @Input() data!: any; // Volume | Chapter
  isChapter = false;
  chapters: Chapter[] = [];
  seriesVolumes: any[] = [];
  isLoadingVolumes = false;

  formatKeys = Object.keys(MangaFormat);
  


  constructor(private modalService: NgbModal, public modal: NgbActiveModal, public utilityService: UtilityService, public imageService: ImageService, public naturalSort: NaturalSortService) { }

  ngOnInit(): void {
    this.isChapter = this.isObjectChapter(this.data);

    if (this.isChapter) {
      this.chapters.push(this.data);
    } else if (!this.isChapter) {
      this.chapters.push(...this.data?.chapters);
    }
    this.chapters.sort(this.utilityService.sortChapters);
    this.chapters.forEach((c: Chapter) => {
      c.files = c.files.sort((a: MangaFile, b: MangaFile) => this.naturalSort.compare(a.filePath, b.filePath, true));
    });
  }

  isObjectChapter(object: any): object is Chapter {
    return ('files' in object);
  }

  isObjectVolume(object: any): object is Volume {
    return !('originalName' in object) && !('files' in object);
  }

  close() {
    this.modal.close();
  }

  formatChapterNumber(chapter: Chapter) {
    if (chapter.number === '0') {
      return '1';
    }
    return chapter.number;
  }
}
