import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {AccountService} from "../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {ConfirmService} from "../../shared/confirm.service";
import {ModalService} from "../../_services/modal.service";
import {NgTemplateOutlet} from "@angular/common";
import {FileSystemFileEntry, NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {ReadingListService} from "../../_services/reading-list.service";
import {ReadingList} from "../../_models/reading-list";
import {ThemeProvider} from "../../_models/preferences/site-theme";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {BrowseCblRepoModalComponent} from "../_modals/browse-cbl-repo-modal/browse-cbl-repo-modal.component";
import {CblService} from "../../_services/cbl.service";
import {CblRepoItem} from "../../_models/reading-list/cbl/cbl-repo-item";

@Component({
  selector: 'app-cbl-manager',
  imports: [
    NgTemplateOutlet,
    NgxFileDropModule,
    LoadingComponent,
    TranslocoDirective
  ],
  templateUrl: './cbl-manager.component.html',
  styleUrl: './cbl-manager.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CblManagerComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(ModalService);
  private readonly readingListService = inject(ReadingListService);
  private readonly cblService = inject(CblService);

  files: NgxFileDropEntry[] = [];
  acceptableExtensions = ['.css'].join(',');
  uploadMode = signal<'file' | 'url' | 'all'>('all');
  isUploadingCbl = signal<boolean>(false);
  allLists = signal<ReadingList[]>([]);

  selectedList = signal<ReadingList | undefined>(undefined);
  showUploadFlow = computed(() => this.selectedList() === undefined);

  ngOnInit() {
    this.readingListService.getReadingLists(false).subscribe(lists => {
      this.allLists.set(lists.result);
    })
  }

  openBrowseModal() {
    this.selectedList.set(undefined);
    const ref = this.modalService.open(BrowseCblRepoModalComponent);
    ref.closed.subscribe((selected: CblRepoItem[]) => {
      if (!selected || selected.length === 0) return;
      this.isUploadingCbl.set(true);
      this.cblService.importFromRepo(selected).subscribe({
        next: () => {
          this.toastr.success(`Imported ${selected.length} reading list(s) from repo`);
          this.isUploadingCbl.set(false);
        },
        error: () => {
          this.toastr.error('Failed to import from repo');
          this.isUploadingCbl.set(false);
        }
      });
    });
  }

  selectList(list: ReadingList | undefined) {
    this.selectedList.set(list);
  }

  public dropped(files: NgxFileDropEntry[]) {
    this.files = files;
    this.isUploadingCbl.set(true);

    for (const droppedFile of files) {
      if (!droppedFile.fileEntry.isFile) continue;
      const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;

      fileEntry.file((file: File) => {
        this.isUploadingCbl.set(false);
        // this.themeService.uploadTheme(file, droppedFile).subscribe(t => {
        //   this.isUploadingTheme = false;
        //   this.downloadedThemes.push(t);
        //   this.selectTheme(t);
        //   this.cdRef.markForCheck();
        // });
      });
    }
  }


  protected readonly ThemeProvider = ThemeProvider;
}
