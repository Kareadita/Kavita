import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {Stack} from "../../../shared/data-structures/stack";
import {TranslocoDirective} from "@jsverse/transloco";
import {CblService} from "../../../_services/cbl.service";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {CblRepoItem} from "../../../_models/reading-list/cbl/cbl-repo-item";
import {GithubRateLimit} from "../../../_models/common/github-rate-limit";
import {CblRepoBrowseResult} from "../../../_models/reading-list/cbl/cbl-repo-browse-result";


@Component({
  selector: 'app-browse-cbl-repo-modal',
  imports: [
    TranslocoDirective,
    LoadingComponent
  ],
  templateUrl: './browse-cbl-repo-modal.component.html',
  styleUrl: './browse-cbl-repo-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrowseCblRepoModalComponent implements OnInit {
  protected readonly modal = inject(NgbActiveModal);
  private readonly cblService = inject(CblService);

  items = signal<CblRepoItem[]>([]);
  selectedItems = signal<CblRepoItem[]>([]);
  loading = signal(false);
  rateLimit = signal<GithubRateLimit | null>(null);
  fromCache = signal(false);

  routeStack = new Stack<string>();
  routeItems = signal<string[]>([]);
  routeStackPeek = computed(() => {
    const items = this.routeItems();
    return items.length > 0 ? items[items.length - 1] : undefined;
  });

  folders = computed(() => this.items().filter(i => i.isDirectory));
  files = computed(() => this.items().filter(i => !i.isDirectory));
  hasSelection = computed(() => this.selectedItems().length > 0);
  selectionCount = computed(() => this.selectedItems().length);

  allFilesSelected = computed(() => {
    const f = this.files();
    if (f.length === 0) return false;
    const sel = this.selectedItems();
    return f.every(file => sel.some(s => s.path === file.path));
  });

  ngOnInit() {
    this.loadDirectory('');
  }

  navigateTo(index: number) {
    while (this.routeStack.items.length - 1 > index) {
      this.routeStack.pop();
    }
    this.syncRouteItems();
    this.loadDirectory(this.routeStack.items.join('/'));
  }

  openFolder(folder: CblRepoItem) {
    this.routeStack.push(folder.name);
    this.syncRouteItems();
    this.loadDirectory(folder.path);
  }

  goBack() {
    this.routeStack.pop();
    this.syncRouteItems();
    this.loadDirectory(this.routeStack.items.join('/'));
  }

  toggleFileSelection(file: CblRepoItem) {
    this.selectedItems.update(current => {
      if (current.some(s => s.path === file.path)) {
        return current.filter(s => s.path !== file.path);
      }
      return [...current, file];
    });
  }

  toggleAllFiles() {
    const files = this.files();
    if (this.allFilesSelected()) {
      const paths = new Set(files.map(f => f.path));
      this.selectedItems.update(current => current.filter(s => !paths.has(s.path)));
    } else {
      this.selectedItems.update(current => {
        const existing = new Set(current.map(s => s.path));
        return [...current, ...files.filter(f => !existing.has(f.path))];
      });
    }
  }

  isSelected(file: CblRepoItem): boolean {
    return this.selectedItems().some(s => s.path === file.path);
  }

  download() {
    this.modal.close(this.selectedItems());
  }

  close() {
    this.modal.dismiss();
  }

  private loadDirectory(path: string) {
    this.loading.set(true);

    this.cblService.browseRepo(path).subscribe({
      next: (result: CblRepoBrowseResult) => {
        this.items.set(result.items);
        this.rateLimit.set(result.rateLimit);
        this.fromCache.set(result.fromCache);
        this.loading.set(false);
      },
      error: () => {
        // Revert navigation on error
        this.routeStack.pop();
        this.syncRouteItems();
        this.loading.set(false);
      },
    });
  }

  private syncRouteItems() {
    this.routeItems.set([...this.routeStack.items]);
  }
}
