import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {NgTemplateOutlet} from "@angular/common";
import {PersonRolePipe} from "../../../_pipes/person-role.pipe";
import {Person, PersonRole} from "../../../_models/metadata/person";
import {
  NgbActiveModal,
  NgbNav,
  NgbNavContent,
  NgbNavItem, NgbNavLink,
  NgbNavLinkBase,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {PersonService} from "../../../_services/person.service";
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {CoverImageChooserComponent} from "../../../cards/cover-image-chooser/cover-image-chooser.component";
import {forkJoin} from "rxjs";
import {UploadService} from "../../../_services/upload.service";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {AccountService} from "../../../_services/account.service";
import {User} from "../../../_models/user";
import {ToastrService} from "ngx-toastr";

enum TabID {
  General = 'general-tab',
  CoverImage = 'cover-image-tab',
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
    NgbNavOutlet,
    CoverImageChooserComponent,
    CompactNumberPipe,
    SettingItemComponent,
    NgbNavLink
  ],
  templateUrl: './edit-person-modal.component.html',
  styleUrl: './edit-person-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditPersonModalComponent implements OnInit {

  protected readonly utilityService = inject(UtilityService);
  private readonly modal = inject(NgbActiveModal);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly personService = inject(PersonService);
  private readonly uploadService = inject(UploadService);
  protected readonly accountService = inject(AccountService);
  protected readonly toastr = inject(ToastrService);

  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;

  @Input({required: true}) person!: Person;

  active = TabID.General;
  editForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
    description: new FormControl('', []),
    asin: new FormControl('', []),
    aniListId: new FormControl('', []),
    malId: new FormControl('', []),
    hardcoverId: new FormControl('', []),
  });

  imageUrls: Array<string> = [];
  selectedCover: string = '';
  coverImageReset = false;
  touchedCoverImage = false;
  fetchDisabled: boolean = false;

  ngOnInit() {
    if (this.person) {
      this.editForm.get('name')!.setValue(this.person.name);
      this.editForm.get('description')!.setValue(this.person.description);
      this.editForm.get('asin')!.setValue((this.person.asin || ''));
      this.editForm.get('aniListId')!.setValue((this.person.aniListId || '')  + '') ;
      this.editForm.get('malId')!.setValue((this.person.malId || '')  + '');
      this.editForm.get('hardcoverId')!.setValue(this.person.hardcoverId || '');

      this.editForm.addControl('coverImageIndex', new FormControl(0, []));
      this.editForm.addControl('coverImageLocked', new FormControl(this.person.coverImageLocked, []));

      this.cdRef.markForCheck();
    } else {
      alert('no person')
    }
  }


  close() {
    this.modal.close({success: false, coverImageUpdate: false});
  }

  save() {
    const apis = [];

    const hasCoverChanges = this.touchedCoverImage || this.coverImageReset;

    if (hasCoverChanges) {
      apis.push(this.uploadService.updatePersonCoverImage(this.person.id, this.selectedCover, !this.coverImageReset));
    }

    const person: Person = {
      id: this.person.id,
      coverImageLocked: this.person.coverImageLocked,
      name: this.editForm.get('name')!.value || '',
      description: this.editForm.get('description')!.value || '',
      asin: this.editForm.get('asin')!.value || '',
      // @ts-ignore
      aniListId: this.editForm.get('aniListId')!.value === '' ? null : parseInt(this.editForm.get('aniListId').value, 10),
      // @ts-ignore
      malId: this.editForm.get('malId')!.value === '' ? null : parseInt(this.editForm.get('malId').value, 10),
      hardcoverId: this.editForm.get('hardcoverId')!.value || '',
    };
    apis.push(this.personService.updatePerson(person));

    forkJoin(apis).subscribe(_ => {
      this.modal.close({success: true, coverImageUpdate: hasCoverChanges, person: person});
    });
  }

  handleUploadByUrl(urls: Array<string>) {
    this.selectedCover = urls[0];
    this.touchedCoverImage = true;
    this.cdRef.markForCheck();
  }

  updateSelectedIndex(index: number) {
    this.editForm.patchValue({
      coverImageIndex: index
    });
    this.touchedCoverImage = true;
    this.cdRef.markForCheck();
  }

  updateSelectedImage(url: string) {
    this.selectedCover = url;
    this.touchedCoverImage = true;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.editForm.patchValue({
      coverImageLocked: false
    });
    this.touchedCoverImage = true;
    this.cdRef.markForCheck();
  }

  downloadCover() {
    this.personService.downloadCover(this.person.id).subscribe(imgUrl => {
      if (imgUrl) {
        this.toastr.success(translate('toasts.person-image-downloaded'));
        this.fetchDisabled = true;
        this.imageUrls.push(imgUrl);
        this.cdRef.markForCheck();
      }
    });
  }

}
