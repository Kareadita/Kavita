import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { Chapter } from 'src/app/_models/chapter';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';

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


  constructor(private modalService: NgbModal, public modal: NgbActiveModal) { }

  ngOnInit(): void {
    this.isChapter = this.isObjectChapter(this.data);

    if (this.isChapter) {
      this.chapters.push(this.data);
    } else {
      this.chapters.push(...this.data?.chapters);
    }

    console.log('Is Chapter: ', this.isChapter);
    console.log(this.data);

  }

  isObjectChapter(object: any): object is Chapter {
    return ('files' in object);
  }

  isObjectVolume(object: any): object is Volume {
    return !('originalName' in object);
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
