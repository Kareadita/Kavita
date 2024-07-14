import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, Inject, inject, OnInit} from '@angular/core';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {FontService} from "src/app/_services/font.service";
import {AccountService} from "../../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {ConfirmService} from "../../../shared/confirm.service";
import {EpubFont, FontProvider} from 'src/app/_models/preferences/epub-font';
import {User} from "../../../_models/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {shareReplay} from "rxjs/operators";
import {map} from "rxjs";
import {NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {AsyncPipe, DOCUMENT, NgIf, NgStyle, NgTemplateOutlet} from "@angular/common";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {SiteThemeProviderPipe} from "../../../_pipes/site-theme-provider.pipe";
import {CarouselReelComponent} from "../../../carousel/_components/carousel-reel/carousel-reel.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {ImageComponent} from "../../../shared/image/image.component";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";

@Component({
  selector: 'app-font-manager',
  imports: [
    TranslocoDirective,
    AsyncPipe,
    LoadingComponent,
    NgxFileDropModule,
    FormsModule,
    NgIf,
    ReactiveFormsModule,
    SentenceCasePipe,
    SiteThemeProviderPipe,
    NgTemplateOutlet,
    NgStyle,
    CarouselReelComponent,
    DefaultValuePipe,
    ImageComponent,
    SafeUrlPipe
  ],
  templateUrl: './font-manager.component.html',
  styleUrl: './font-manager.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
})
export class FontManagerComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly fontService = inject(FontService);
  private readonly accountService = inject(AccountService);
  public readonly fb = inject(FormBuilder);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);

  protected readonly FontProvider = FontProvider;

  user: User | undefined;
  fonts: Array<EpubFont> = [];
  hasAdmin$ = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef), shareReplay({refCount: true, bufferSize: 1}),
    map(c => c && this.accountService.hasAdminRole(c))
  );

  form: FormGroup = new FormGroup({
    fontUrl: new FormControl('', [])
  });

  selectedFont: EpubFont | undefined = undefined;

  files: NgxFileDropEntry[] = [];
  acceptableExtensions = ['.woff2', 'woff', 'tff', 'otf'].join(',');
  mode: 'file' | 'url' | 'all' = 'all';
  isUploadingFont: boolean = false;


  constructor(@Inject(DOCUMENT) private document: Document) {}

  ngOnInit() {
    this.loadFonts();
  }

  loadFonts() {
    this.fontService.getFonts().subscribe(fonts => {
      this.fonts = fonts;
      this.cdRef.markForCheck();
    });
  }

  selectFont(font: EpubFont | undefined) {
    if (!font) {
      this.selectedFont = undefined;
      this.cdRef.markForCheck();
      return;
    }

    this.fontService.getFontFace(font).load().then(loadedFace => {
      (this.document as any).fonts.add(loadedFace);
    });
    this.selectedFont = font;
    this.cdRef.markForCheck();
  }

  dropped(files: NgxFileDropEntry[]) {
    for (const droppedFile of files) {
      if (!droppedFile.fileEntry.isFile) {
        continue;
      }

      const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
      fileEntry.file((file: File) => {
        this.fontService.uploadFont(file, droppedFile).subscribe(f => {
          this.isUploadingFont = false;
          this.fonts = [...this.fonts, f];
          this.cdRef.markForCheck();
        });
      });
    }
    this.isUploadingFont = true;
    this.cdRef.markForCheck();
  }

  uploadFromUrl() {
    const url = this.form.get('fontUrl')?.value.trim();
    if (!url || url === '') return;

    this.fontService.uploadFromUrl(url).subscribe(() => {
      this.loadFonts();
    });
  }

  async deleteFont(id: number) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-font'))) {
      return;
    }

    this.fontService.deleteFont(id).subscribe(() => {
      this.fonts = this.fonts.filter(f => f.id !== id);
      this.cdRef.markForCheck();
    });
  }

  changeMode(mode: 'file' | 'url' | 'all') {
    this.mode = mode;
    this.cdRef.markForCheck();
  }

}
