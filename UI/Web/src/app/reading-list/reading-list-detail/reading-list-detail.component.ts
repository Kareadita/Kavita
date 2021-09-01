import { Component, OnInit } from '@angular/core';
import { Chapter } from 'src/app/_models/chapter';

@Component({
  selector: 'app-reading-list-detail',
  templateUrl: './reading-list-detail.component.html',
  styleUrls: ['./reading-list-detail.component.scss']
})
export class ReadingListDetailComponent implements OnInit {

  chapters: Array<{
    seriesName: string,
    chapterNumber: number,
    volumeNumber: number,
    read: boolean,
    order: number
  }> = [];
  constructor( ) { }

  ngOnInit(): void {
    this.chapters.push(this.createChapter('Death Note', 0));
    this.chapters.push(this.createChapter('Death Note', 1));
    this.chapters.push(this.createChapter('Aria', 2));
    this.chapters.push(this.createChapter('Btooom!', 3));
  }

  createChapter(title: string, order: number) {
    return {
      seriesName: title,
      chapterNumber: Math.round(Math.random() * 100 + 1),
      volumeNumber: 0,
      read: Math.random() > 0.5,
      order
    };
  }

}
