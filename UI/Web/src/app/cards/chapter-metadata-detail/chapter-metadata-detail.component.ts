import { Component, Input, OnInit } from '@angular/core';
import { MetadataService } from 'src/app/_services/metadata.service';
import { Chapter } from 'src/app/_models/chapter';
import { ChapterMetadata } from 'src/app/_models/chapter-metadata';
import { UtilityService } from 'src/app/shared/_services/utility.service';

@Component({
  selector: 'app-chapter-metadata-detail',
  templateUrl: './chapter-metadata-detail.component.html',
  styleUrls: ['./chapter-metadata-detail.component.scss']
})
export class ChapterMetadataDetailComponent implements OnInit {

  @Input() chapter!: Chapter;
  metadata!: ChapterMetadata;

  constructor(private metadataService: MetadataService, public utilityService: UtilityService) { }

  ngOnInit(): void {
    this.metadataService.getChapterMetadata(this.chapter.id).subscribe(metadata => {
      console.log('Chapter ', this.chapter.number, ' metadata: ', metadata);
      this.metadata = metadata;
    })
  }

}
