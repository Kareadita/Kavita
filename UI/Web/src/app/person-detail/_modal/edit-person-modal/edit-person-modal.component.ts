import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit, ViewChild} from '@angular/core';
import {UtilityService} from "../../../shared/_services/utility.service";
import {
  AbstractControl,
  AsyncValidatorFn,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from "@angular/forms";
import {Person, PersonRole} from "../../../_models/metadata/person";
import {
  NgbActiveModal,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavLinkBase,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {PersonService} from "../../../_services/person.service";
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {CoverImageChooserComponent} from "../../../cards/cover-image-chooser/cover-image-chooser.component";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../../_services/cover-chooser-config-factory.service";
import {concat, map, of} from "rxjs";
import {UploadService} from "../../../_services/upload.service";
import {ImageService} from "../../../_services/image.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {AccountService} from "../../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {EditListComponent} from "../../../shared/edit-list/edit-list.component";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";

@Component({
  selector: 'app-edit-person-modal',
  imports: [
    ReactiveFormsModule,
    NgbNav,
    NgbNavItem,
    TranslocoDirective,
    NgbNavLinkBase,
    NgbNavContent,
    NgbNavOutlet,
    CoverImageChooserComponent,
    SettingItemComponent,
    NgbNavLink,
    EditListComponent,
    TabTitlePipe
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
  private readonly imageService = inject(ImageService);
  protected readonly accountService = inject(AccountService);
  protected readonly toastr = inject(ToastrService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  protected readonly Tabs = Tabs;

  @Input({required: true}) person!: Person;
  @ViewChild(CoverImageChooserComponent) coverChooser?: CoverImageChooserComponent;

  active = Tabs.General;
  editForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
    description: new FormControl('', []),
    asin: new FormControl('', [], [this.amazonCodeValidator()]),
    aniListId: new FormControl('', []),
    malId: new FormControl('', []),
    hardcoverId: new FormControl('', []),
  });

  selectedCover: string = '';
  coverImageReset = false;
  coverImageDirty = false;
  chooserConfig: CoverImageChooserConfig = {};
  fetchDisabled: boolean = false;
  /**
   * Suffix to include in the tooltip for external ids if they support characters
   */
  tooltip: string = '';


  ngOnInit() {
    if (this.person) {
      this.editForm.get('name')!.setValue(this.person.name);
      this.editForm.get('description')!.setValue(this.person.description);
      this.editForm.get('asin')!.setValue((this.person.asin || ''));
      this.editForm.get('aniListId')!.setValue((this.person.aniListId || '')  + '') ;
      this.editForm.get('malId')!.setValue((this.person.malId || '')  + '');
      this.editForm.get('hardcoverId')!.setValue(this.person.hardcoverId || '');

      this.editForm.addControl('coverImageLocked', new FormControl(this.person.coverImageLocked, []));
      this.chooserConfig = this.coverChooserConfigFactory.forPerson(this.person);

      const roles = (this.person.roles ?? []);
      if (roles.length === 1 && roles.includes(PersonRole.Character)) {
        this.tooltip = '-character';
      }

      this.cdRef.markForCheck();
    } else {
      alert('no person')
    }
  }


  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.person, true));
    } else {
      this.modal.dismiss();
    }
  }

  save() {
    const apis = [];

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
      aliases: this.person.aliases,
    };
    apis.push(this.personService.updatePerson(person));

    const hasCoverChanges = this.coverImageDirty || this.coverImageReset;
    if (this.coverImageDirty) {
      apis.push(this.uploadService.updatePersonCoverImage(this.person.id, this.selectedCover, true));
    }

    // Run api calls in sequency to prevent them from overwriting each-other in a race condition
    concat(...apis).subscribe(_ => {
      this.modal.close(modalSaved(person, hasCoverChanges));
    });
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
    this.cdRef.markForCheck();
  }

  handleReset() {
    this.coverImageReset = true;
    this.editForm.patchValue({ coverImageLocked: false });
    this.chooserConfig = { ...this.chooserConfig, isLocked: false };
    this.cdRef.markForCheck();
  }

  downloadCover() {
    this.personService.downloadCover(this.person.id).subscribe(imgUrl => {
      if (imgUrl) {
        this.toastr.success(translate('toasts.person-image-downloaded'));
        this.fetchDisabled = true;
        this.coverChooser?.addImage({ url: imgUrl, title: translate('cover-image-chooser.download-cover') });
        this.cdRef.markForCheck();
      }
    });
  }

  aliasValidator(): AsyncValidatorFn {
    return (control: AbstractControl) => {
      const alias = control.value;
      if (!alias || alias.trim().length === 0) {
        return of(null);
      }

      const name = this.editForm.get('name')!.value;
      return this.personService.isValidAlias(this.person.id, alias, name).pipe(map(valid => {
        if (valid) {
          return null;
        }

        return { 'invalidAlias': {'alias': alias} } as ValidationErrors;
      }));
    }
  }

  /**
   * Validates that the string is a high probability of being an asin
   */
  amazonCodeValidator(): AsyncValidatorFn {
    return (control: AbstractControl) => {
      const asin = control.value;
      if (!asin || asin.trim().length === 0) {
        return of(null);
      }

      //https://stackoverflow.com/questions/2123131/determine-if-10-digit-string-is-valid-amazon-asin
      if (!asin.toUpperCase().startsWith('B0') || !/^(B0|BT)[0-9A-Z]{8}$/.test(asin.toUpperCase())) {
        return of({ 'amazonCode': {'asin': asin} } as ValidationErrors);
      }

      return of(null);
    }
  }

}
