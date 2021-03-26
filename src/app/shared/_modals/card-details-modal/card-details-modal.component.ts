import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFile } from 'src/app/_models/manga-file';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import { ImageService } from 'src/app/_services/image.service';
import { SeriesService } from 'src/app/_services/series.service';
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


  constructor(private modalService: NgbModal, public modal: NgbActiveModal, public utilityService: UtilityService, public imageService: ImageService) { }

  ngOnInit(): void {
    this.isChapter = this.isObjectChapter(this.data);

    if (this.isChapter) {
      this.chapters.push(this.data);
    } else if (!this.isChapter) {
      this.chapters.push(...this.data?.chapters);
    }
    this.chapters.sort(this.utilityService.sortChapters);
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
