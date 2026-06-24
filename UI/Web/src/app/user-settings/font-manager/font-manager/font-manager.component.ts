import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {FontService} from "src/app/_services/font.service";
import {AccountService} from "../../../_services/account.service";
import {ConfirmService} from "../../../shared/confirm.service";
import {EpubFont, FontProvider} from 'src/app/_models/preferences/epub-font';
import {NgxFileDropEntry} from "ngx-file-drop";
import {DOCUMENT} from "@angular/common";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {SiteThemeProviderPipe} from "../../../_pipes/site-theme-provider.pipe";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {ToastrService} from "ngx-toastr";
import {
  FileDragAndDropUploadComponent
} from "src/app/shared/file-drag-and-drop-upload/file-drag-and-drop-upload.component";
import {NgbCollapse, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {tap} from "rxjs";
import {finalize} from "rxjs/operators";
import {TranslocoInjectComponent} from "../../../shared/_components/transloco-inject/transloco-inject.component";
import {TranslocoSlotDirective} from "../../../_directives/transloco-slot.directive";

/**
 * A family groups every uploaded/system file that shares the same family name (the name the user picks
 * while reading). Each family may hold multiple files for its weights and styles.
 */
export interface FontFamilyGroup {
  family: string;
  provider: FontProvider;
  files: EpubFont[];
  /** True when any file declares a weight range (variable font), e.g. "400 800". */
  isVariable: boolean;
  weightMin: number;
  weightMax: number;
  hasItalic: boolean;
  hasNormal: boolean;
  /** Whether the family can be visually previewed (the special Default font cannot). */
  canPreview: boolean;
  /** Namespaced css family name used only for the preview so it can never clobber a global family. */
  previewAlias: string;
}

@Component({
  selector: 'app-font-manager',
  imports: [
    LoadingComponent,
    SentenceCasePipe,
    SiteThemeProviderPipe,
    TranslocoDirective,
    FileDragAndDropUploadComponent,
    NgbCollapse,
    NgbTooltip,
    TranslocoInjectComponent,
    TranslocoSlotDirective
  ],
  templateUrl: './font-manager.component.html',
  styleUrl: './font-manager.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true
})
export class FontManagerComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  protected readonly fontService = inject(FontService);

  protected readonly isReadOnly = this.accountService.hasReadOnlyRole;

  fonts = signal<EpubFont[]>([]);
  hideSystemFonts = signal(false);

  selectedFamilyKey = signal<string | undefined>(undefined);
  isUploadingFont = signal(false);

  /** Toggles the inline upload zone in the header. */
  showUpload = signal(false);
  /** Toggles the "files in this family" disclosure in the specimen panel. */
  showFiles = signal(false);
  /** Current weight applied to the preview (drives the variable weight axis slider). */
  currentWeight = signal(400);

  visibleFonts = computed(() => {
    // Sort fonts in provider order (System then Custom)
    const fonts = [...this.fonts()].sort((a, b) => a.provider - b.provider);
    if (!this.hideSystemFonts()) return fonts;

    return fonts.filter(f => f.provider === FontProvider.User);
  });

  fontFamilies = computed<FontFamilyGroup[]>(() => {
    const groups = new Map<string, EpubFont[]>();
    for (const font of this.visibleFonts()) {
      const existing = groups.get(font.family);
      if (existing) {
        existing.push(font);
      } else {
        groups.set(font.family, [font]);
      }
    }

    return [...groups.entries()].map(([family, files]) => this.buildGroup(family, files));
  });

  selectedGroup = computed<FontFamilyGroup | undefined>(() => {
    const key = this.selectedFamilyKey();
    if (key === undefined) return undefined;
    return this.fontFamilies().find(g => g.family === key);
  });

  // When accepting more types, also need to update in the Parser
  acceptableExtensions = ['.woff2', '.woff', '.ttf', '.otf'].join(',');

  ngOnInit() {
    this.loadFonts();
  }

  loadFonts() {
    this.fontService.getFonts().subscribe(fonts => {
      this.fonts.set(fonts);

      // First load, if there are user provided fonts, switch the filter toggle
      if (fonts.some(f => f.provider !== FontProvider.System) && !this.hideSystemFonts()) {
        this.hideSystemFonts.set(true);
      }

      const families = this.fontFamilies();
      if (families.length > 0) {
        this.selectFamily(families[0]);
      }
    });
  }

  private buildGroup(family: string, files: EpubFont[]): FontFamilyGroup {
    const isVariable = files.some(f => this.isVariableWeight(f.weight));
    const variableFile = files.find(f => this.isVariableWeight(f.weight));
    const range = variableFile ? this.parseWeightRange(variableFile.weight) : {min: 400, max: 700};
    const canPreview = files.some(f => f.name !== FontService.DefaultEpubFont);

    return {
      family,
      provider: files[0].provider,
      files,
      isVariable,
      weightMin: range.min,
      weightMax: range.max,
      hasItalic: files.some(f => f.style === 'italic'),
      hasNormal: files.some(f => f.style !== 'italic'),
      canPreview,
      previewAlias: `kavita-preview-${files[0].id}`,
    };
  }

  isVariableWeight(weight: string): boolean {
    return /\s/.test((weight ?? '').trim());
  }

  parseWeightRange(weight: string): {min: number, max: number} {
    const parts = weight.trim().split(/\s+/).map(Number);
    return {min: parts[0] || 100, max: parts[1] || 900};
  }

  /** Renders a file's weight as a range ("400–800") for variable fonts, or the plain number otherwise. */
  weightLabel(weight: string): string {
    if (!this.isVariableWeight(weight)) return weight;
    const {min, max} = this.parseWeightRange(weight);
    return `${min}–${max}`;
  }

  selectFamily(group: FontFamilyGroup) {
    this.selectedFamilyKey.set(group.family);
    this.showFiles.set(false);

    // Default the preview weight: variable fonts start at 400 (clamped to range), otherwise the
    // weight of the family's normal file.
    if (group.isVariable) {
      this.currentWeight.set(Math.min(Math.max(400, group.weightMin), group.weightMax));
    } else {
      const normal = group.files.find(f => f.style !== 'italic') ?? group.files[0];
      this.currentWeight.set(Number(normal.weight) || 400);
    }

    if (!group.canPreview) return;

    // Register every file in the family under a single namespaced alias so the browser can pick the
    // correct face by style/weight. The alias never matches a real family, so global styles are untouched.
    for (const font of group.files) {
      if (font.name === FontService.DefaultEpubFont) continue;
      this.fontService.getFontFace(font, group.previewAlias).load()
        .then(loadedFace => (this.document as any).fonts.add(loadedFace))
        .catch(() => { /* preview only; ignore load failures */ });
    }
  }

  toggleCustomOnly() {
    this.hideSystemFonts.update(x => !x);

    // Keep the selection valid against the new filter
    if (!this.selectedGroup()) {
      const families = this.fontFamilies();
      if (families.length > 0) this.selectFamily(families[0]);
      else this.selectedFamilyKey.set(undefined);
    }
  }

  toggleUpload() {
    this.showUpload.update(x => !x);
  }

  dropped(files: NgxFileDropEntry[]) {
    for (const droppedFile of files) {
      if (!droppedFile.fileEntry.isFile) {
        continue;
      }

      const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
      fileEntry.file((file: File) => {
        this.fontService.uploadFont(file, droppedFile).subscribe(f => {
          this.addFont(f);
          this.isUploadingFont.set(false);
        });
      });
    }

    this.isUploadingFont.set(true);
  }

  uploadFromUrl(url: string) {
    this.isUploadingFont.set(true);
    this.fontService.uploadFromUrl(url).pipe(
      tap(fonts => fonts.forEach(font => this.addFont(font))),
      finalize(() => this.isUploadingFont.set(false))
    ).subscribe();
  }

  async deleteFamily(group: FontFamilyGroup) {
    if (group.provider === FontProvider.System) return;

    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-font'))) {
      return;
    }

    this.requestDeleteFamily(group);
  }

  // The reader selects a family as a whole, so the backend deletes (and validates in-use for) the whole family
  // from any one of its files. A non-forced delete is rejected when in use; admins can then confirm a force delete.
  private requestDeleteFamily(group: FontFamilyGroup, force: boolean = false) {
    const userFile = group.files.find(f => f.provider === FontProvider.User);
    if (!userFile) return;

    this.fontService.deleteFontFamily(userFile.id, force).subscribe(async (result) => {
      if (result.deleted) {
        this.onFamilyDeleted(group);
        return;
      }

      if (!result.inUse) return;

      if (!this.accountService.hasAdminRole()) {
        this.toastr.info(translate('toasts.font-in-use'));
        return;
      }

      if (!await this.confirmService.confirm(translate('toasts.confirm-force-delete-font'))) {
        return;
      }
      this.requestDeleteFamily(group, true);
    });
  }

  private onFamilyDeleted(group: FontFamilyGroup) {
    const ids = group.files.filter(f => f.provider === FontProvider.User).map(f => f.id);
    this.fonts.update(x => x.filter(f => !ids.includes(f.id)));

    const families = this.fontFamilies();
    if (families.length === 0 && this.hideSystemFonts()) {
      this.hideSystemFonts.set(false);
      const all = this.fontFamilies();
      if (all.length > 0) this.selectFamily(all[0]);
      return;
    }

    if (families.length > 0) {
      this.selectFamily(families[families.length - 1]);
    } else {
      this.selectedFamilyKey.set(undefined);
    }
  }

  private addFont(font: EpubFont) {
    // Check if the font is already in our list. Bail out if it is.
    if (this.fonts().some(f => f.id === font.id)) {
      return;
    }
    this.fonts.update(x => [...x, font]);

    const group = this.fontFamilies().find(g => g.family === font.family);
    if (group) {
      setTimeout(() => this.selectFamily(group), 100);
    }
  }

  protected readonly FontProvider = FontProvider;
  protected readonly WikiLink = WikiLink.EpubFontManager;
}
