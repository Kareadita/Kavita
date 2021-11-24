import { Component, Input, OnInit } from '@angular/core';
import { MetadataService } from 'src/app/_services/metadata.service';
import { Chapter } from 'src/app/_models/chapter';

@Component({
  selector: 'app-chapter-metadata-detail',
  templateUrl: './chapter-metadata-detail.component.html',
  styleUrls: ['./chapter-metadata-detail.component.scss']
})
export class ChapterMetadataDetailComponent implements OnInit {

  @Input() chapter!: Chapter;

  constructor(private metadataService: MetadataService) { }

  ngOnInit(): void {
    this.metadataService.getChapterMetadata(this.chapter.id).subscribe(metadata => {
      console.log('Chapter ', this.chapter.number, ' metadata: ', metadata);
    })
  }

}
