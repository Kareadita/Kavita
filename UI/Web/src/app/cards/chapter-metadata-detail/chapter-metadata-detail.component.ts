import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { ChapterMetadata } from 'src/app/_models/metadata/chapter-metadata';

@Component({
  selector: 'app-chapter-metadata-detail',
  templateUrl: './chapter-metadata-detail.component.html',
  styleUrls: ['./chapter-metadata-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterMetadataDetailComponent {
  @Input() chapter: ChapterMetadata | undefined;

  constructor() { }
}
