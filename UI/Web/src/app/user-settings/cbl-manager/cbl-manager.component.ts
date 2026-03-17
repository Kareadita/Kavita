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
import {ReadingList, ReadingListProvider} from '../../_models/reading-list';
import {LoadingComponent} from "../../shared/loading/loading.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {BrowseCblRepoModalComponent} from "../_modals/browse-cbl-repo-modal/browse-cbl-repo-modal.component";
import {CblService} from '../../_services/cbl.service';
import {CblRepoItem} from '../../_models/reading-list/cbl/cbl-repo-item';
import {CblImportResult} from '../../_models/reading-list/cbl/cbl-import-result.enum';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {PromotedIconComponent} from '../../shared/_components/promoted-icon/promoted-icon.component';
import {ReadingListProviderPipe} from "../../_pipes/reading-list-provider.pipe";

@Component({
  selector: 'app-cbl-manager',
  imports: [
    NgTemplateOutlet,
    NgxFileDropModule,
    LoadingComponent,
    TranslocoDirective,
    ReactiveFormsModule,
    PromotedIconComponent,
    ReadingListProviderPipe
  ],
  templateUrl: './cbl-manager.component.html',
  styleUrl: './cbl-manager.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CblManagerComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly ReadingListProvider = ReadingListProvider;
  protected readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(ModalService);
  private readonly readingListService = inject(ReadingListService);
  private readonly cblService = inject(CblService);

  form = new FormGroup({
    cblUrl: new FormControl('', [])
  });
  files: NgxFileDropEntry[] = [];
  acceptableExtensions = ['.cbl', '.json'].join(',');
  uploadMode = signal<'file' | 'url' | 'all'>('all');
  isUploadingCbl = signal<boolean>(false);
  allLists = signal<ReadingList[]>([]);

  selectedList = signal<ReadingList | undefined>(undefined);
  showUploadFlow = computed(() => this.selectedList() === undefined);

  searchTerm = signal<string>('');
  providerFilter = signal<ReadingListProvider | null>(null);
  hasUpdateFilter = signal<boolean>(false);

  filteredLists = computed(() => {
    let lists = this.allLists();
    const term = this.searchTerm().toLowerCase().trim();
    const provider = this.providerFilter();
    const hasUpdate = this.hasUpdateFilter();

    if (term) {
      lists = lists.filter(l => l.title.toLowerCase().includes(term));
    }
    if (provider !== null) {
      lists = lists.filter(l => l.provider === provider);
    }
    if (hasUpdate) {
      lists = lists.filter(l => l.hasRemoteChange);
    }
    return lists;
  });

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
        this.cblService.importFromFile(file, droppedFile).subscribe({
          next: (summary) => {
            if (summary.success === CblImportResult.Fail) {
              this.toastr.error('Failed to import CBL file');
            } else {
              this.toastr.success('Imported reading list from file');
            }
            this.isUploadingCbl.set(false);
            this.files = [];
            this.refreshLists();
          },
          error: () => {
            this.toastr.error('Failed to upload CBL file');
            this.isUploadingCbl.set(false);
            this.files = [];
          }
        });
      });
    }
  }

  uploadFromUrl() {
    const url = this.form.get('cblUrl')?.value?.trim();
    if (!url) return;

    this.isUploadingCbl.set(true);
    this.cblService.importFromUrl(url).subscribe({
      next: (summary) => {
        // TODO: I will hook this up with the importer modal next, temp code
        this.form.get('cblUrl')!.setValue('');
        this.isUploadingCbl.set(false);
        this.refreshLists();
      },
      error: () => {
        this.toastr.error('Failed to download CBL file');
        this.isUploadingCbl.set(false);
      }
    });
  }

  setProviderFilter(provider: ReadingListProvider | null) {
    this.providerFilter.set(this.providerFilter() === provider ? null : provider);
  }

  private refreshLists() {
    this.readingListService.getReadingLists(false).subscribe(lists => {
      this.allLists.set(lists.result);
    });
  }
}
