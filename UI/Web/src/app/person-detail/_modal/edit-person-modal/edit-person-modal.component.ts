import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {NgTemplateOutlet} from "@angular/common";
import {PersonRolePipe} from "../../../_pipes/person-role.pipe";
import {Person} from "../../../_models/metadata/person";
import {
  NgbActiveModal,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLinkBase,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@ngneat/transloco";

enum TabID {
  General = 0,
  CoverImage = 1,
}

@Component({
  selector: 'app-edit-person-modal',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    NgTemplateOutlet,
    PersonRolePipe,
    NgbNav,
    NgbNavItem,
    TranslocoDirective,
    NgbNavLinkBase,
    NgbNavContent,
    NgbNavOutlet
  ],
  templateUrl: './edit-person-modal.component.html',
  styleUrl: './edit-person-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditPersonModalComponent implements OnInit {

  protected readonly utilityService = inject(UtilityService);
  private readonly modal = inject(NgbActiveModal);
  private readonly cdRef = inject(ChangeDetectorRef);

  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;

  @Input({required: true}) person!: Person;

  tabs = ['general-tab', 'cover-image-tab'];
  active = this.tabs[0];
  editForm = new FormGroup({
    name: new FormControl('', Validators.required),
    description: new FormControl('', []),
    asin: new FormControl('', []),
    aniListId: new FormControl('', []),
    malId: new FormControl('', []),
    hardcoverId: new FormControl('', []),
  });

  ngOnInit() {
    if (this.person) {
      this.editForm.get('name')!.setValue(this.person.name);
      this.editForm.get('description')!.setValue(this.person.description);
      this.editForm.get('asin')!.setValue(this.person.asin || '');
      this.editForm.get('aniListId')!.setValue((this.person.aniListId + '') || '');
      this.editForm.get('malId')!.setValue((this.person.malId + '') || '');
      this.editForm.get('hardcoverId')!.setValue(this.person.hardcoverId || '');
      this.cdRef.markForCheck();
    }
  }


  close() {
    this.modal.close({success: false, coverImageUpdate: false});
  }

  save() {
    this.modal.close({success: true, coverImageUpdate: false});
  }

}
