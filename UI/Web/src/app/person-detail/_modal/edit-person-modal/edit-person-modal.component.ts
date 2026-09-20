import {ChangeDetectionStrategy, Component, computed, inject, model, OnInit, signal} from '@angular/core';
import {Person, PersonRole} from "../../../_models/metadata/person";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {PersonService} from "../../../_services/person.service";
import {TranslocoDirective} from '@jsverse/transloco';
import {CoverImageChooserComponent} from "../../../cards/cover-image-chooser/cover-image-chooser.component";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../../_services/cover-chooser-config-factory.service";
import {map, Observable, of, switchMap} from "rxjs";
import {UploadService} from "../../../_services/upload.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {EditListComponent} from "../../../shared/edit-list/edit-list.component";
import {modalSaved} from "../../../_models/modal/modal-result";
import {Tabs} from "../../../_models/tabs";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {form, FormField, required, ValidationResult} from "@angular/forms/signals";
import {EditModalShellComponent} from "../../../shared/edit-modal-shell/edit-modal-shell.component";
import {EditTabDirective} from "../../../shared/_directive/edit-tab.directive";
import {amazonCode} from "../../../_validators/amazon-code.validator";

interface FormModel {
  name: string;
  description: string;
  asin: string;
  aniListId: number | null;
  malId: number | null;
  hardcoverId: string;
  aliases: string[];
  coverImageLocked: boolean;
}

@Component({
  selector: 'app-edit-person-modal',
  imports: [
    TranslocoDirective,
    CoverImageChooserComponent,
    SettingItemComponent,
    EditListComponent,
    FormFieldDirective,
    FormField,
    EditModalShellComponent,
    EditTabDirective
  ],
  templateUrl: './edit-person-modal.component.html',
  styleUrl: './edit-person-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditPersonModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly personService = inject(PersonService);
  private readonly uploadService = inject(UploadService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);

  person = model.required<Person>();

  protected readonly activeId = signal(Tabs.General);
  private selectedCover = '';
  private coverImageReset = false;
  private coverImageDirty = false;

  private readonly formModel = signal<FormModel>({
    name: '',
    description: '',
    asin: '',
    aniListId: null,
    malId: null,
    hardcoverId: '',
    aliases: [],
    coverImageLocked: false,
  });

  protected readonly formGroup = form(this.formModel, p => {
    required(p.name);
    amazonCode(p.asin);
  });

  protected readonly chooserConfig = computed<CoverImageChooserConfig>(() => ({
    ...this.coverChooserConfigFactory.forPerson(this.person()),
    isLocked: this.formGroup.coverImageLocked().value()
  }));

  /**
   * Suffix to include in the tooltip for external ids if they support characters
   */
  protected readonly tooltip = computed(() => {
    const roles = this.person().roles ?? [];
    return roles.length === 1 && roles.includes(PersonRole.Character) ? '-character' : '';
  });

  ngOnInit() {
    // Seeded once. A linkedSignal here would wipe in-progress edits whenever person() changes
    this.formModel.set({
      name: this.person().name,
      description: this.person().description,
      asin: this.person().asin || '',
      aniListId: this.person().aniListId ?? null,
      malId: this.person().malId ?? null,
      hardcoverId: this.person().hardcoverId || '',
      aliases: this.person().aliases ?? [],
      coverImageLocked: this.person().coverImageLocked,
    });
  }

  close() {
    if (this.coverImageReset) {
      this.modal.close(modalSaved(this.person(), true));
    } else {
      this.modal.dismiss();
    }
  }

  save() {
    const model = this.formModel();

    const payload: Person = {
      ...this.person(),
      ...model,
      aniListId: model.aniListId ?? undefined,
      malId: model.malId ?? undefined,
    };

    this.personService.updatePerson(payload).pipe(
      switchMap(person => this.coverImageDirty
        ? this.uploadService.updatePersonCoverImage(this.person().id, this.selectedCover, true).pipe(map(() => person))
        : of(person))
    ).subscribe(person => {
      this.person.set(person);
      const needsCoverUpdate = this.coverImageDirty || this.coverImageReset;
      this.modal.close(modalSaved(this.person(), needsCoverUpdate));
    });
  }

  updateAliases(aliases: string[]) {
    this.formGroup.aliases().value.set(aliases);
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    this.coverImageDirty = event.isDirty;
    this.selectedCover = event.fileName;
  }

  handleReset() {
    this.coverImageReset = true;
    this.formGroup.coverImageLocked().value.set(false);
  }

  aliasValidator = (alias: string): Observable<ValidationResult> => {
    if (!alias || alias.trim().length === 0) {
      return of(null);
    }

    return this.personService.isValidAlias(this.person().id, alias, this.formGroup.name().value()).pipe(
      map(valid => valid ? null : [{kind: 'invalidAlias'}])
    );
  }

  protected readonly Tabs = Tabs;

}
