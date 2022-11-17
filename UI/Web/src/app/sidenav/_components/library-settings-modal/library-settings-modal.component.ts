import { Component, Input, OnInit } from '@angular/core';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { Library } from 'src/app/_models/library';
import { UploadService } from 'src/app/_services/upload.service';

enum TabID {
  General = 'General',
  Cover = 'Cover',
  Advanced = 'Advanced'
}

@Component({
  selector: 'app-library-settings-modal',
  templateUrl: './library-settings-modal.component.html',
  styleUrls: ['./library-settings-modal.component.scss']
})
export class LibrarySettingsModalComponent implements OnInit {

  @Input() library!: Library;

  active = TabID.General;
  imageUrls: Array<string> = [];

  get Breakpoint() { return Breakpoint; }
  get TabID() { return TabID; }

  constructor(public utilityService: UtilityService, private uploadService: UploadService) { }

  ngOnInit(): void {
    if (this.library.coverImage != null && this.library.coverImage !== '') {
      this.imageUrls.push(this.library.coverImage);
    }
  }

  close() {}
  save() {}

  applyCoverImage(coverUrl: string) {
    this.uploadService.updateLibraryCoverImage(this.library.id, coverUrl).subscribe(() => {});
  }

  resetCoverImage() {
    this.uploadService.updateLibraryCoverImage(this.library.id, '').subscribe(() => {});
  }

}
