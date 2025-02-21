import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import {distinctUntilChanged, map, take, tap} from 'rxjs';
import { ThemeService } from 'src/app/_services/theme.service';
import {SiteTheme, ThemeProvider} from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SentenceCasePipe } from '../../_pipes/sentence-case.pipe';
import { AsyncPipe, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {shareReplay} from "rxjs/operators";
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ImageComponent} from "../../shared/image/image.component";
import {DownloadableSiteTheme} from "../../_models/theme/downloadable-site-theme";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {ConfirmService} from "../../shared/confirm.service";
import {FileSystemFileEntry, NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {PreviewImageModalComponent} from "../../shared/_components/carousel-modal/preview-image-modal.component";
import {DefaultModalOptions} from "../../_models/default-modal-options";

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
    standalone: true,
  imports: [AsyncPipe, SentenceCasePipe, TranslocoDirective, CarouselReelComponent,
    ImageComponent, DefaultValuePipe, NgTemplateOutlet, NgxFileDropModule,
    ReactiveFormsModule, Select2Module, LoadingComponent]
})
export class ThemeManagerComponent {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly themeService = inject(ThemeService);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(NgbModal);

  protected readonly ThemeProvider = ThemeProvider;
  protected readonly ScrobbleProvider = ScrobbleProvider;

  currentTheme: SiteTheme | undefined;
  user: User | undefined;
  selectedTheme: ThemeContainer | undefined;
  downloadableThemes: Array<DownloadableSiteTheme> = [];
  downloadedThemes: Array<SiteTheme> = [];
  hasAdmin$ = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef),
    map(c => c && this.accountService.hasAdminRole(c)),
    shareReplay({refCount: true, bufferSize: 1}),
  );

  canUseThemes$ = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef),
    map(c => c && !this.accountService.hasReadOnlyRole(c)),
    shareReplay({refCount: true, bufferSize: 1}),
  );

  files: NgxFileDropEntry[] = [];
  acceptableExtensions = ['.css'].join(',');
  isUploadingTheme: boolean = false;



  constructor() {

    this.themeService.themes$.pipe(tap(themes => {
      this.downloadedThemes = themes;
      this.cdRef.markForCheck();
    })).subscribe();

    this.loadDownloadableThemes();

    this.themeService.currentTheme$.pipe(takeUntilDestroyed(this.destroyRef), distinctUntilChanged()).subscribe(theme => {
      this.currentTheme = theme;
      this.cdRef.markForCheck();
    });

  }

  loadDownloadableThemes() {
    this.themeService.getDownloadableThemes().subscribe(d => {
      this.downloadableThemes = d;
      this.cdRef.markForCheck();
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
    this.selectedTheme = undefined;
    this.downloadableThemes = this.downloadableThemes.filter(d => d.name !== theme.name);
    this.cdRef.markForCheck();
  }

  applyTheme(theme: SiteTheme) {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (!user) return;
      const pref = Object.assign({}, user.preferences);
      pref.theme = theme;
      this.accountService.updatePreferences(pref).subscribe();
      // Updating theme emits the new theme to load on the themes$
    });
  }

  updateDefault(theme: SiteTheme) {
    this.themeService.setDefault(theme.id).subscribe(() => {
      this.toastr.success(translate('theme-manager.updated-toastr', {name: theme.name}));
    });
  }

  selectTheme(theme: SiteTheme | DownloadableSiteTheme | undefined) {
    if (theme === undefined) {
      this.selectedTheme = undefined;
      return;
    }

    if (theme.hasOwnProperty('provider')) {
      this.selectedTheme = {
        isSiteTheme: true,
        site: theme as SiteTheme,
        name: theme.name
      };
    } else {
      this.selectedTheme = {
        isSiteTheme: false,
        downloadable: theme as DownloadableSiteTheme,
        name: theme.name
      };
    }

    this.cdRef.markForCheck();
  }

  downloadTheme(theme: DownloadableSiteTheme) {
    this.themeService.downloadTheme(theme).subscribe(downloadedTheme => {
      this.removeDownloadedTheme(downloadedTheme);
      this.themeService.getThemes().subscribe(themes => {
        this.downloadedThemes = themes;
        const oldTheme = this.downloadedThemes.filter(d => d.name === theme.name)[0];
        this.selectTheme(oldTheme);
        this.cdRef.markForCheck();
      });

    });
  }

  public dropped(files: NgxFileDropEntry[]) {
    this.files = files;
    for (const droppedFile of files) {
      // Is it a file?
      if (droppedFile.fileEntry.isFile) {
        const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
        fileEntry.file((file: File) => {
          this.themeService.uploadTheme(file, droppedFile).subscribe(t => {
            this.isUploadingTheme = false;
            this.cdRef.markForCheck();
          });
        });
      }
    }
    this.isUploadingTheme = true;
    this.cdRef.markForCheck();
  }

  previewImage(imgUrl: string) {
    if (imgUrl === '') return;

    const ref = this.modalService.open(PreviewImageModalComponent, DefaultModalOptions);
    ref.componentInstance.title = this.selectedTheme!.name;
    ref.componentInstance.image = imgUrl;
  }
}
