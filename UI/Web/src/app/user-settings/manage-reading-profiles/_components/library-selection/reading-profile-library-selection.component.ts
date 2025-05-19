import {Component, Input, OnInit} from '@angular/core';
import {FormGroup} from "@angular/forms";
import {ReadingProfile} from "../../../../_models/preferences/reading-profiles";
import {Library} from "../../../../_models/library/library";
import {LibraryService} from "../../../../_services/library.service";
import {ReadingProfileService} from "../../../../_services/reading-profile.service";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-reading-profile-library-selection',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './reading-profile-library-selection.component.html',
  styleUrl: './reading-profile-library-selection.component.scss'
})
export class ReadingProfileLibrarySelectionComponent implements OnInit{

  @Input({required: true}) readingProfileForm!: FormGroup;
  @Input({required: true}) selectedProfile!: ReadingProfile;

  allLibraries: Library[] = []

  constructor(private libraryService: LibraryService, private readingProfileService: ReadingProfileService) {
  }

  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libs => this.allLibraries = libs);
  }

  addToProfile(library: Library) {
    this.readingProfileService.addToLibrary(this.selectedProfile.id, library.id).subscribe()
  }

  removeFromProfile(library: Library) {
    this.readingProfileService.removeFromLibrary(this.selectedProfile.id, library.id).subscribe()
  }

}
