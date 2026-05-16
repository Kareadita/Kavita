import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {TranslocoDirective} from '@jsverse/transloco';
import {NgbNav, NgbNavItem, NgbNavLink} from '@ng-bootstrap/ng-bootstrap';
import {KavitaPlusAuditService} from '../../_services/kavitaplus-audit.service';
import {ScrobbleProvider, ScrobblingService, UserScrobbleProvider} from '../../_services/scrobbling.service';
import {KavitaPlusAuditEntry} from '../../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditCategory} from '../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {AuditStatus} from '../../_models/kavitaplus/audit-status.enum';
import {ActivityTabId} from '../../_models/kavitaplus/activity-tab-id.enum';
import {KavitaplusTimelineComponent} from '../../_components/kavitaplus-timeline/kavitaplus-timeline.component';
import {ScrobbleAccountCardComponent} from '../scrobble-account-card/scrobble-account-card.component';

@Component({
  selector: 'app-kavitaplus-activity',
  templateUrl: './kavitaplus-activity.component.html',
  styleUrls: ['./kavitaplus-activity.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, NgbNav, NgbNavItem, NgbNavLink, KavitaplusTimelineComponent, ScrobbleAccountCardComponent],
})
export class KavitaplusActivityComponent implements OnInit {
  private readonly auditService = inject(KavitaPlusAuditService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly destroyRef = inject(DestroyRef);

  entries = signal<KavitaPlusAuditEntry[]>([]);
  isLoading = signal(true);
  activeTab = signal<ActivityTabId>(ActivityTabId.All);
  scrobblingProviders = signal<UserScrobbleProvider[]>([]);

  allCount      = computed(() => this.entries().length);
  scrobbleCount = computed(() => this.entries().filter(e => e.category === KavitaPlusAuditCategory.Scrobble).length);
  failedCount   = computed(() => this.entries().filter(e => e.status === AuditStatus.Failure).length);
  myChangesCount = computed(() => this.entries().filter(e => e.userId != null).length);

  filteredEntries = computed(() => {
    const tab = this.activeTab();
    const all = this.entries();
    if (tab === ActivityTabId.Scrobbles) return all.filter(e => e.category === KavitaPlusAuditCategory.Scrobble);
    if (tab === ActivityTabId.Failed)    return all.filter(e => e.status === AuditStatus.Failure);
    if (tab === ActivityTabId.MyChanges) return all.filter(e => e.userId != null);
    return all;
  });

  ngOnInit() {
    this.auditService.getMyActivity({}, undefined, 200)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => { this.entries.set(result.result ?? []); this.isLoading.set(false); },
        error: ()     => this.isLoading.set(false),
      });

    this.scrobblingService.getScrobbleProviders().subscribe(tokens => this.scrobblingProviders.set(tokens));
  }

  protected readonly ActivityTabId = ActivityTabId;
  protected readonly ScrobbleProvider = ScrobbleProvider;
}
