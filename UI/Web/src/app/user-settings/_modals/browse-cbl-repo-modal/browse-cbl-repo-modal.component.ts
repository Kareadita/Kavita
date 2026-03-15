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
  selectedItems = signal<Set<string>>(new Set());
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
  hasSelection = computed(() => this.selectedItems().size > 0);
  selectionCount = computed(() => this.selectedItems().size);

  allFilesSelected = computed(() => {
    const f = this.files();
    if (f.length === 0) return false;
    const sel = this.selectedItems();
    return f.every(file => sel.has(file.path));
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
      const next = new Set(current);
      if (next.has(file.path)) {
        next.delete(file.path);
      } else {
        next.add(file.path);
      }
      return next;
    });
  }

  toggleAllFiles() {
    const files = this.files();
    if (this.allFilesSelected()) {
      this.selectedItems.update(current => {
        const next = new Set(current);
        for (const file of files) {
          next.delete(file.path);
        }
        return next;
      });
    } else {
      this.selectedItems.update(current => {
        const next = new Set(current);
        for (const file of files) {
          next.add(file.path);
        }
        return next;
      });
    }
  }

  isSelected(file: CblRepoItem): boolean {
    return this.selectedItems().has(file.path);
  }

  download() {
    const selected = this.items().filter(i => this.selectedItems().has(i.path));
    this.modal.close(selected);
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
