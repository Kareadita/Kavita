import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  input,
  OnInit,
  signal,
  output
} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {UtilityService} from "../../../../shared/_services/utility.service";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {AccountService} from "../../../../_services/account.service";
import {Observable} from "rxjs";
import {ActionableEntity} from "../../../../_services/action-factory.service";
import {ActionItem} from "../../../../_models/actionables/action-item";
import {Action} from "../../../../_models/actionables/action";
import {ActionResult} from "../../../../_models/actionables/action-result";

@Component({
    selector: 'app-actionable-modal',
    imports: [
        TranslocoDirective
    ],
    templateUrl: './actionable-modal.component.html',
    styleUrl: './actionable-modal.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActionableModalComponent implements OnInit {

  protected readonly utilityService = inject(UtilityService);
  protected readonly modal = inject(NgbActiveModal);
  protected readonly accountService = inject(AccountService);
  protected readonly cdRef = inject(ChangeDetectorRef);
  protected readonly destroyRef = inject(DestroyRef);


  entity = input<ActionableEntity>(null);
  /** This assumes these are filtered actions */
  filteredActions = input<ActionItem<any>[]>([]);
  readonly actionPerformed = output<ActionItem<any> | ActionResult<any>>();

  currentItems = signal<ActionItem<any>[]>([]);
  currentLevel = signal<string[]>([]);

  ngOnInit() {
    // Copy as the list may be shared between entities
    const actionItems = this.surfaceDownloadAction(
      this.filteredActions().map(a => this.utilityService.copyActionItem(a))
    );
    this.currentItems.set(this.translateOptions(actionItems));
  }

  handleItemClick(item: ActionItem<any>) {
    if (item.children?.length > 0) {
      this.currentLevel.update(levels => [...levels, item.title]);

      if (item.children.length === 1 && item.children[0].dynamicList) {
        item.children[0].dynamicList.subscribe(dynamicItems => {
          this.currentItems.set(dynamicItems.map(di => ({
            ...item,
            children: [], // Required as dynamic list is only one deep
            title: di.title,
            _extra: di,
            action: item.children[0].action // override action to be correct from child
          })));
        });
      } else {
        this.currentItems.set(this.translateOptions(item.children));
      }
      return;
    }

    const result = item.callback(item, this.entity());

    if (result && typeof (result as any).subscribe === 'function') {
      (result as Observable<ActionResult<any>>).subscribe(actionResult => {
        this.actionPerformed.emit(actionResult);
        this.modal.close(actionResult);
      });
      return;
    }
    this.modal.close(item);
  }

  handleBack() {
    this.currentLevel.update(levels => {
      const next = levels.slice(0, -1);

      let items = this.filteredActions().map(a => this.utilityService.copyActionItem(a));
      items = this.surfaceDownloadAction(items);

      for (const level of next) {
        items = items.find(i => i.title === level)?.children || [];
      }

      this.currentItems.set(this.translateOptions(items));
      return next;
    });
  }

  translateOptions(opts: Array<ActionItem<any>>) {
    return opts.map(a => {
      return {...a, title: translate('actionable.' + a.title)};
    })
  }

  /**
   * On mobile, pull the Download action out of the "Others" submenu to top-level.
   */
  private surfaceDownloadAction(items: ActionItem<any>[]): ActionItem<any>[] {
    const otherIdx = items.findIndex(i => i.action === Action.Submenu && i.title === 'others');
    if (otherIdx < 0) return items;

    const dlIdx = items[otherIdx].children.findIndex(a => a.action === Action.Download);
    if (dlIdx < 0) return items;

    const downloadAction = items[otherIdx].children.splice(dlIdx, 1)[0];
    items.push(downloadAction);

    if (items[otherIdx].children.length === 0) {
      items.splice(otherIdx, 1);
    }

    return items;
  }
}
