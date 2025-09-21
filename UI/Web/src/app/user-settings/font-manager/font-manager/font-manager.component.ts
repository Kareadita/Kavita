import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {FontService} from "src/app/_services/font.service";
import {AccountService} from "../../../_services/account.service";
import {ConfirmService} from "../../../shared/confirm.service";
import {EpubFont, FontProvider} from 'src/app/_models/preferences/epub-font';
import {NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {DOCUMENT, NgStyle, NgTemplateOutlet} from "@angular/common";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {SiteThemeProviderPipe} from "../../../_pipes/site-theme-provider.pipe";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {animate, style, transition, trigger} from "@angular/animations";
import {WikiLink} from "../../../_models/wiki";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-font-manager',
  imports: [
    LoadingComponent,
    NgxFileDropModule,
    FormsModule,
    ReactiveFormsModule,
    SentenceCasePipe,
    SiteThemeProviderPipe,
    NgTemplateOutlet,
    TranslocoDirective,
    NgStyle,
  ],
  templateUrl: './font-manager.component.html',
  styleUrl: './font-manager.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  animations: [
    trigger('loadNewFontAnimation', [
      transition('void => loaded', [
        style({ backgroundColor: 'var(--primary-color)' }),
        animate('2s', style({ backgroundColor: 'var(--list-group-item-bg-color)' }))
      ])
    ])
  ],
})
export class FontManagerComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  protected readonly fontService = inject(FontService);

  protected readonly FontProvider = FontProvider;
  protected readonly WikiLink = WikiLink.EpubFontManager;
  protected readonly user = this.accountService.currentUserSignal;
  protected readonly isReadOnly = computed(() => {
    const u = this.accountService.currentUserSignal();
    if (!u) return true;

    return this.accountService.hasReadOnlyRole(u);
  });

  fonts = signal<EpubFont[]>([]);
  visibleFonts = computed(() => {
    const fonts = this.fonts();
    const hide = this.hideSystemFonts();
    if (!hide) return fonts;

    return fonts.filter(f => f.provider === FontProvider.User);
  });

  hideSystemFonts = signal(false);


  /**
   * Fonts added during the current sessions
   */
  loadedFonts = signal<EpubFont[]>([]);

  selectedFont = signal<EpubFont | undefined>(undefined);
  isUploadingFont = signal(false);
  uploadMode = signal<'file' | 'url' | 'all'>('all');

  form: FormGroup = new FormGroup({
    fontUrl: new FormControl('', []),
    filter: new FormControl(this.hideSystemFonts(), [])
  });

  files: NgxFileDropEntry[] = [];
  // When accepting more types, also need to update in the Parser
  acceptableExtensions = ['.woff2', '.woff', '.ttf', '.otf'].join(',');

  ngOnInit() {
    this.loadFonts();
  }

  loadFonts() {
    this.fontService.getFonts().subscribe(fonts => {
      this.fonts.set(fonts);

      // First load, if there are user provided fonts, switch the filter toggle
      if (fonts.filter(f => f.provider != FontProvider.System).length > 0 && !this.hideSystemFonts()) {
        this.setHideSystemFontsFilter(true);
      }
    });
  }

  selectFont(font: EpubFont | undefined) {
    if (font === undefined) {
      this.selectedFont.set(font);
      return;
    }


    if (font.name !== FontService.DefaultEpubFont) {
      this.fontService.getFontFace(font).load().then(loadedFace => {
        (this.document as any).fonts.add(loadedFace);
        this.selectedFont.set(font);
      });
    } else {
      this.selectedFont.set(font);
    }
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

  uploadFromUrl() {
    const url = this.form.get('fontUrl')?.value.trim();
    if (!url || url === '') return;

    this.isUploadingFont.set(true);
    this.fontService.uploadFromUrl(url).subscribe((f) => {
      this.addFont(f);
      this.form.get('fontUrl')!.setValue('');
      this.isUploadingFont.set(false);
    });
  }

  async deleteFont(id: number) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-font'))) {
      return;
    }

    // Check if this font is in use
    this.fontService.isFontInUse(id).subscribe(async (inUse) => {
      if (!inUse) {
        this.performDeleteFont(id);
        return;
      }

      const isAdmin = this.accountService.hasAdminRole(this.accountService.currentUserSignal()!);

      if (!isAdmin) {
        this.toastr.info(translate('toasts.font-in-use'))
        return;
      }

      if (!await this.confirmService.confirm(translate('toasts.confirm-force-delete-font'))) {
        return;
      }
      this.performDeleteFont(id, true);
    })


  }

  private setHideSystemFontsFilter(value: boolean) {
    this.hideSystemFonts.set(value);
    this.form.get('filter')?.setValue(value);
  }

  private performDeleteFont(id: number, force: boolean = false) {
    this.fontService.deleteFont(id, force).subscribe(() => {
      this.fonts.update(x => x.filter(f => f.id !== id));

      // Select the first font in the list
      const visibleFonts = this.visibleFonts();
      if (visibleFonts.length === 0 && this.hideSystemFonts()) {
        this.setHideSystemFontsFilter(false);
        this.selectFont(this.fonts()[0]); // Default
        return;
      }

      if (visibleFonts.length > 0) {
        this.selectFont(visibleFonts[visibleFonts.length - 1]);
      }
    });
  }

  private addFont(font: EpubFont) {
    this.fonts.update(x => [...x, font]);
    this.loadedFonts.update(x => [...x, font]);
    setTimeout(() => this.selectedFont.set(font), 100);
  }

  animationState(font: EpubFont) {
    return this.loadedFonts().includes(font) ? 'loaded' : '';
  }

  protected readonly FontService = FontService;
}
