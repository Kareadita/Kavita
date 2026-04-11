import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {AccountService} from '../../_services/account.service';
import {ToastrService} from 'ngx-toastr';
import {ConfirmService} from '../../shared/confirm.service';
import {ModalService} from '../../_services/modal.service';
import {NgTemplateOutlet} from '@angular/common';
import {FileSystemFileEntry, NgxFileDropEntry, NgxFileDropModule} from 'ngx-file-drop';
import {ReadingListService} from '../../_services/reading-list.service';
import {ReadingList, ReadingListProvider} from '../../_models/reading-list/reading-list';
import {LoadingComponent} from '../../shared/loading/loading.component';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {BrowseCblRepoModalComponent} from '../_modals/browse-cbl-repo-modal/browse-cbl-repo-modal.component';
import {ImportCblModalComponent} from '../_modals/import-cbl-modal/import-cbl-modal.component';
import {CblService} from '../../_services/cbl.service';
import {CblRepoItem} from '../../_models/reading-list/cbl/cbl-repo-item';
import {CblSavedFile} from '../../_models/reading-list/cbl/cbl-saved-file';
import {ReactiveFormsModule} from '@angular/forms';
import {PromotedIconComponent} from '../../shared/_components/promoted-icon/promoted-icon.component';
import {ReadingListProviderPipe} from '../../_pipes/reading-list-provider.pipe';
import {forkJoin} from 'rxjs';
import {ImageService} from '../../_services/image.service';
import {ReadMoreComponent} from '../../shared/read-more/read-more.component';
import {ImageComponent} from '../../shared/image/image.component';
import {RouterLink} from '@angular/router';
import {editModal} from "../../_models/modal/modal-options";
import {ModalResult} from "../../_models/modal/modal-result";
import {
  FileDragAndDropUploadComponent
} from "src/app/shared/file-drag-and-drop-upload/file-drag-and-drop-upload.component";
import {UtcToLocalDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
import {TimeAgoPipe} from "../../_pipes/time-ago.pipe";
import {AgeRatingImageComponent} from "../../_single-module/age-rating-image/age-rating-image.component";
import {DateYearRangePipe} from "../../_pipes/date-year-range.pipe";
import {SafeUrlPipe} from "../../_pipes/safe-url.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-cbl-manager',
  imports: [
    NgTemplateOutlet,
    NgxFileDropModule,
    LoadingComponent,
    TranslocoDirective,
    ReactiveFormsModule,
    PromotedIconComponent,
    ReadingListProviderPipe,
    ReadMoreComponent,
    ImageComponent,
    RouterLink,
    FileDragAndDropUploadComponent,
    UtcToLocalDatePipe,
    TimeAgoPipe,
    AgeRatingImageComponent,
    SafeUrlPipe,
    NgbTooltip
  ],
  templateUrl: './cbl-manager.component.html',
  styleUrl: './cbl-manager.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CblManagerComponent implements OnInit {
  protected readonly ReadingListProvider = ReadingListProvider;
  protected readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(ModalService);
  private readonly readingListService = inject(ReadingListService);
  private readonly cblService = inject(CblService);
  protected readonly imageService = inject(ImageService);

  files: NgxFileDropEntry[] = [];
  acceptableExtensions = ['.cbl', '.json'].join(',');
  private readonly dateYearRangePipe = new DateYearRangePipe();
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
    });
  }

  openBrowseModal() {
    this.selectedList.set(undefined);
    const ref = this.modalService.open(BrowseCblRepoModalComponent);
    ref.closed.subscribe((selected: CblRepoItem[]) => {
      if (!selected || selected.length === 0) return;
      this.isUploadingCbl.set(true);
      this.cblService.importFromRepo(selected).subscribe({
        next: (savedFiles) => {
          this.isUploadingCbl.set(false);
          this.openImportModal(savedFiles);
        },
        error: () => {
          this.toastr.error('Failed to download from repo');
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

    const uploads$ = files
      .filter(f => f.fileEntry.isFile)
      .map(droppedFile => {
        return new Promise<ReturnType<typeof this.cblService.importFromFile>>((resolve) => {
          const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
          fileEntry.file((file: File) => {
            resolve(this.cblService.importFromFile(file, droppedFile));
          });
        });
      });

    Promise.all(uploads$).then(observables => {
      forkJoin(observables).subscribe({
        next: (savedFiles) => {
          this.isUploadingCbl.set(false);
          this.files = [];
          this.openImportModal(savedFiles);
        },
        error: () => {
          this.toastr.error('Failed to upload CBL file(s)');
          this.isUploadingCbl.set(false);
          this.files = [];
        }
      });
    });
  }

  uploadFromUrl(url: string) {
    this.isUploadingCbl.set(true);
    this.cblService.importFromUrl(url).subscribe({
      next: (savedFile) => {
        this.isUploadingCbl.set(false);
        this.openImportModal([savedFile]);
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

  syncReadingList(list: ReadingList) {
    this.cblService.syncList(list.id, true).subscribe(() => {
      this.toastr.success(translate('toasts.reading-list-sync-enqueued'));
    });
  }

  getDateRangeLabel(rl: ReadingList) {
    if (!rl || rl.startingYear === 0) return null;

    // Reading list dates start with 1, JS Date starts with 0
    const startMonth = rl.startingMonth > 0 ? rl.startingMonth - 1 : undefined;
    const endMonth = rl.startingMonth > 0 ? rl.endingMonth - 1 : undefined;

    const startDate = startMonth !== undefined ? new Date(rl.startingYear, startMonth) : new Date(rl.startingYear);
    const endDate = rl.endingYear <= 0 ? null :
      (endMonth !== undefined ? new Date(rl.endingYear, endMonth) : new Date(rl.endingYear));

    return this.dateYearRangePipe.transform(startDate, endDate, !!endMonth);
  }

  getMissingCount(rl: ReadingList) {
    if (rl.totalItemsAtImport === 0) return 0;
    if (rl.itemCount > rl.totalItemsAtImport) return 0;
    return rl.totalItemsAtImport - rl.itemCount;
  }

  async deleteList(list: ReadingList) {
    const confirmed = await this.confirmService.confirm(translate('toasts.confirm-delete-reading-list'));
    if (!confirmed) return;
    this.readingListService.delete(list.id).subscribe(() => {
      this.selectedList.set(undefined);
      this.refreshLists();
      this.toastr.success(translate('toasts.reading-list-deleted'));
    });
  }

  private openImportModal(savedFiles: CblSavedFile[]) {
    const ref = this.modalService.open(ImportCblModalComponent, editModal());
    ref.setInput('savedFiles', savedFiles);
    ref.closed.subscribe((res: ModalResult) => {
      this.refreshLists();
      this.selectedList.set(undefined);
    });
  }

  private refreshLists() {
    this.readingListService.getReadingLists(false).subscribe(lists => {
      this.allLists.set([...lists.result]);
    });
  }
}
