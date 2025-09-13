import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  Inject,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {FontService} from "src/app/_services/font.service";
import {AccountService} from "../../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {ConfirmService} from "../../../shared/confirm.service";
import {EpubFont, FontProvider} from 'src/app/_models/preferences/epub-font';
import {User} from "../../../_models/user";
import {NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {DOCUMENT, NgStyle, NgTemplateOutlet} from "@angular/common";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {SiteThemeProviderPipe} from "../../../_pipes/site-theme-provider.pipe";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {animate, style, transition, trigger} from "@angular/animations";

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
    NgStyle,
    TranslocoDirective,
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
  private document = inject(DOCUMENT);
  protected readonly fontService = inject(FontService);
  private readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);

  protected readonly FontProvider = FontProvider;

  user = this.accountService.currentUserSignal;
  fonts = signal<EpubFont[]>([]);
  /**
   * Fonts added during the current sessions
   */
  loadedFonts = signal<EpubFont[]>([]);

  selectedFont = signal<EpubFont | undefined>(undefined);
  isUploadingFont = signal(false);

  form: FormGroup = new FormGroup({
    fontUrl: new FormControl('', [])
  });

  files: NgxFileDropEntry[] = [];
  acceptableExtensions = ['.woff2', 'woff', 'tff', 'otf'].join(',');
  mode: 'file' | 'url' | 'all' = 'all';

  ngOnInit() {
    this.loadFonts();
  }

  loadFonts() {
    this.fontService.getFonts().subscribe(fonts => {
      this.fonts.set(fonts);
    });
  }

  selectFont(font: EpubFont | undefined) {
    if (font === undefined) {
      this.selectedFont.set(font);
      return;
    }


    if (font.name !== 'Default') {
      this.fontService.getFontFace(font).load().then(loadedFace => {
        (this.document as any).fonts.add(loadedFace);
      });
    }


    this.selectedFont.set(font);
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
      this.isUploadingFont.set(false);
    });
  }

  async deleteFont(id: number) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-font'))) {
      return;
    }

    this.fontService.deleteFont(id).subscribe(() => {
      this.fonts.update(x => x.filter(f => f.id !== id))
      this.cdRef.markForCheck();
    });
  }

  changeMode(mode: 'file' | 'url' | 'all') {
    this.mode = mode;
    this.cdRef.markForCheck();
  }

  private addFont(font: EpubFont) {
    this.fonts.update(x => [...x, font]);
    this.loadedFonts.update(x => [...x, font]);
  }

  animationState(font: EpubFont) {
    return this.loadedFonts().includes(font) ? 'loaded' : '';
  }

}
