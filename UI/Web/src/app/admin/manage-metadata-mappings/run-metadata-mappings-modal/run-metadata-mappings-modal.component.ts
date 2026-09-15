import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {SettingsService} from "../../settings.service";
import {LibraryService} from "../../../_services/library.service";
import {Library} from "../../../_models/library/library";
import {finalize, tap} from "rxjs/operators";
import {TypeaheadConfig} from "../../../typeahead/_models/typeahead-config";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {of} from "rxjs";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {ToastrService} from '@openng/ngx-toastr';
import {TypeaheadConfigFactoryService} from "../../../typeahead-config-factory.service";
import {RunMetadataMappingsRequest} from "../../../_models/metadata/run-metadata-mappings-request";
import {form, FormField, validate} from "@angular/forms/signals";
@Component({
  selector: 'app-run-metadata-mappings-modal',
  imports: [
    TranslocoDirective,
    TypeaheadComponent,
    SettingItemComponent,
    FormField
  ],
  templateUrl: './run-metadata-mappings-modal.component.html',
  styleUrl: './run-metadata-mappings-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunMetadataMappingsModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly settingsService = inject(SettingsService);
  private readonly libraryService = inject(LibraryService);
  private readonly toastR = inject(ToastrService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadConfigFactoryService);

  libraries = signal<Library[]>([]);
  isSaving = signal(false);
  formModel = signal<RunMetadataMappingsRequest>({
    allLibraries: false,
    includedLibraries: [],
    excludedLibraries: []
  });
  formGroup = form(this.formModel, path => {
    validate(path, ctx => {
      const allLibraries = ctx.stateOf(path.allLibraries).value();
      const includedLibraries = ctx.stateOf(path.includedLibraries).value();

      if (allLibraries || includedLibraries.length > 0) {
        return null;
      }

      return {
        kind: 'librarySelectionRequired'
      };
    })
  });

  includedLibrariesTypeaheadSettings = signal<TypeaheadConfig<Library> | null>(null);
  excludedLibrariesTypeaheadSettings = signal<TypeaheadConfig<Library> | null>(null);

  ngOnInit() {
    this.libraryService.getLibraries().pipe(
      tap(libraries => this.libraries.set(libraries)),
      tap(() => this.setupTypeaheads()),
    ).subscribe();
  }

  private setupTypeaheads() {
    const includedSettings = this.typeaheadSettingsFactory.forLibraries({id: 'included-libraries', libraries: this.libraries(), overrides: {
        fetchFn: (query) => {
          const excludedLibraries = this.formModel().excludedLibraries;

          return of(this.libraries()
            .filter(l => !excludedLibraries.includes(l.id))
            .filter(l => l.name.toLowerCase().includes(query.toLowerCase())));
        },
        dropdownPosition: 'body'
      }
    });

    const excludedSettings = this.typeaheadSettingsFactory.forLibraries({id: 'excluded-libraries', libraries: this.libraries(), overrides: {
        fetchFn: (query) => {
          const includedLibraries = this.formModel().includedLibraries;

          return of(this.libraries()
            .filter(l => !includedLibraries.includes(l.id))
            .filter(l => l.name.toLowerCase().includes(query.toLowerCase())));
        },
        dropdownPosition: 'body'
      }
    });

    this.includedLibrariesTypeaheadSettings.set(includedSettings);
    this.excludedLibrariesTypeaheadSettings.set(excludedSettings);
  }

  protected updateLibrarySelection(field: 'includedLibraries' | 'excludedLibraries', libraries: Library[]) {
    this.formModel.update(x => ({
      ...x,
      [field]: libraries.map(l => l.id),
    }));
  }

  protected close() {
    this.modal.close();
  }

  protected submit() {
    if (!this.formGroup().valid()) return;

    this.settingsService.runMetadataMappings(this.formModel()).pipe(
      tap(() => this.toastR.info(
        translate('run-metadata-mappings-modal.queued-description'),
        translate('run-metadata-mappings-modal.queued-title')
      )),
      finalize(() => this.close())
    ).subscribe();
  }

}
