import { Component, Input, OnInit } from '@angular/core';
import { MangaFile } from 'src/app/_models/manga-file';

@Component({
  selector: 'app-file-info',
  templateUrl: './file-info.component.html',
  styleUrls: ['./file-info.component.scss']
})
export class FileInfoComponent implements OnInit {

  /**
   * MangaFile to display
   */
  @Input() file!: MangaFile;
  /**
   * DateTime the entity this file belongs to was created
   */
  @Input() created: string | undefined = undefined;

  constructor() { }

  ngOnInit(): void {
  }

}
