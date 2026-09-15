import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject, OnInit,
  signal,
} from '@angular/core';
import {ToastrService} from '@openng/ngx-toastr';
import {SentenceCasePipe} from '../../_pipes/sentence-case.pipe';
import {NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ImageComponent} from "../../shared/image/image.component";
import {DownloadableSiteTheme} from "../../_models/theme/downloadable-site-theme";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {ConfirmService} from "../../shared/confirm.service";
import {FileSystemFileEntry, NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {PreviewImageModalComponent} from "../../shared/_components/carousel-modal/preview-image-modal.component";
import {ModalService} from "../../_services/modal.service";
import {SiteTheme, ThemeProvider} from "../../_models/preferences/site-theme";
import {ThemeService} from "../../_services/theme.service";
import {AccountService} from "../../_services/account.service";
import {
  FileDragAndDropUploadComponent
} from "../../shared/file-drag-and-drop-upload/file-drag-and-drop-upload.component";

interface ThemeContainer {
  downloadable?: DownloadableSiteTheme;
  site?: SiteTheme;
  isSiteTheme: boolean;
  name: string;
}

@Component({
    selector: 'app-theme-manager',
    templateUrl: './theme-manager.component.html',
    styleUrls: ['./theme-manager.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SentenceCasePipe, TranslocoDirective, CarouselReelComponent,
    ImageComponent, DefaultValuePipe, NgTemplateOutlet, NgxFileDropModule,
    LoadingComponent, FileDragAndDropUploadComponent]
})
export class ThemeManagerComponent implements OnInit {
  protected readonly themeService = inject(ThemeService);
  protected readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(ModalService);

  protected readonly ThemeProvider = ThemeProvider;
  protected readonly ScrobbleProvider = ScrobbleProvider;

  currentTheme = this.themeService.currentTheme;
  downloadedThemes = this.themeService.themes;

  selectedTheme = signal<ThemeContainer | null>(null);
  downloadableThemes = signal<DownloadableSiteTheme[]>([]);

  canUseThemes = computed(() => !this.accountService.hasReadOnlyRole());

  acceptableExtensions = ['.css'].join(',');
  isUploadingTheme = signal(false);

  ngOnInit() {
    this.loadDownloadableThemes();
  }

  loadDownloadableThemes() {
    this.themeService.getDownloadableThemes().subscribe(d => {
      this.downloadableThemes.set(d);
    });
  }

  async deleteTheme(theme: SiteTheme) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-theme'))) {
      return;
    }

    this.themeService.deleteTheme(theme.id).subscribe(_ => {
      this.removeDownloadedTheme(theme);
      this.loadDownloadableThemes();
    });
  }

  removeDownloadedTheme(theme: SiteTheme) {
    this.selectedTheme.set(null);
    this.downloadableThemes.update(x => [...x.filter(d => d.name !== theme.name)]);
  }

  applyTheme(theme: SiteTheme) {
    const user = this.accountService.currentUser();
    if (!user) return;

    // Updating theme emits the new theme to load on the themes$
    const pref = Object.assign({}, user.preferences);
    pref.theme = theme;
    this.accountService.updatePreferences(pref).subscribe();
  }

  updateDefault(theme: SiteTheme) {
    this.themeService.setDefault(theme.id).subscribe(() => {
      // TODO: Refactor this key to be in toasts
      this.toastr.success(translate('theme-manager.updated-toastr', {name: theme.name}));
    });
  }

  selectTheme(theme: SiteTheme | DownloadableSiteTheme | undefined) {
    if (theme === undefined) {
      this.selectedTheme.set(null);
      return;
    }

    if (Object.hasOwnProperty.call(theme, 'provider')) {
      this.selectedTheme.set({
        isSiteTheme: true,
        site: theme as SiteTheme,
        name: theme.name
      });
    } else {
      this.selectedTheme.set({
        isSiteTheme: false,
        downloadable: theme as DownloadableSiteTheme,
        name: theme.name
      });
    }
  }

  downloadTheme(theme: DownloadableSiteTheme) {
    this.themeService.downloadTheme(theme).subscribe(downloadedTheme => {
      this.removeDownloadedTheme(downloadedTheme);
      this.themeService.getThemes().subscribe(_ => {
        const oldTheme = this.downloadedThemes()?.filter(d => d.name === theme.name)[0];
        this.selectTheme(oldTheme);
      });

    });
  }

  public dropped(files: NgxFileDropEntry[]) {
    this.isUploadingTheme.set(true);

    for (const droppedFile of files) {
      if (!droppedFile.fileEntry.isFile) continue;
      const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;

      fileEntry.file((file: File) => {
        this.themeService.uploadTheme(file, droppedFile).subscribe(t => {
          this.isUploadingTheme.set(false);
          this.selectTheme(t);
        });
      });
    }
  }

  previewImage(imgUrl: string) {
    if (imgUrl === '') return;

    const ref = this.modalService.open(PreviewImageModalComponent);
    ref.setInput('title', this.selectedTheme!.name);
    ref.setInput('image', imgUrl);
  }
}
