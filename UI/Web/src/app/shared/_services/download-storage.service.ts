import {Injectable} from '@angular/core';
import {DownloadQueueItem} from '../_models/download-queue-item';

@Injectable({ providedIn: 'root' })
export class DownloadStorageService {
  private readonly DB_NAME = 'kavita-downloads';
  private readonly STORE = 'download-queue';
  private db: IDBDatabase | null = null;

  /** Opens (or creates) the IndexedDB database and returns all persisted items. */
  async open(): Promise<DownloadQueueItem[]> {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(this.DB_NAME, 1);

      req.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains(this.STORE)) {
          db.createObjectStore(this.STORE, { keyPath: 'id' });
        }
      };

      req.onsuccess = (event) => {
        this.db = (event.target as IDBOpenDBRequest).result;
        const tx = this.db.transaction(this.STORE, 'readonly');
        const store = tx.objectStore(this.STORE);
        const getAllReq = store.getAll();
        getAllReq.onsuccess = () => resolve(getAllReq.result as DownloadQueueItem[]);
        getAllReq.onerror = () => reject(getAllReq.error);
      };

      req.onerror = () => reject(req.error);
    });
  }

  /** Upsert an item by id. Only call on status changes, not on every progress update. */
  async save(item: DownloadQueueItem): Promise<void> {
    if (!this.db) return;
    return new Promise((resolve, reject) => {
      // Strip non-serializable fields before persisting
      const { entity, speedBps, ...persistable } = item as any;
      const tx = this.db!.transaction(this.STORE, 'readwrite');
      const req = tx.objectStore(this.STORE).put(persistable);
      req.onsuccess = () => resolve();
      req.onerror = () => reject(req.error);
    });
  }

  /** Remove a single item by id. */
  async delete(id: number): Promise<void> {
    if (!this.db) return;
    return new Promise((resolve, reject) => {
      const tx = this.db!.transaction(this.STORE, 'readwrite');
      const req = tx.objectStore(this.STORE).delete(id);
      req.onsuccess = () => resolve();
      req.onerror = () => reject(req.error);
    });
  }

  /** Returns all completed items with completedAt before the given timestamp. */
  async getCompletedBefore(timestampMs: number): Promise<DownloadQueueItem[]> {
    if (!this.db) return [];
    return new Promise((resolve, reject) => {
      const tx = this.db!.transaction(this.STORE, 'readonly');
      const store = tx.objectStore(this.STORE);
      const getAllReq = store.getAll();
      getAllReq.onsuccess = () => {
        const all = getAllReq.result as DownloadQueueItem[];
        resolve(all.filter(i => i.status === 'completed' && (i.completedAt ?? 0) < timestampMs));
      };
      getAllReq.onerror = () => reject(getAllReq.error);
    });
  }

  /** Remove all items. */
  async clear(): Promise<void> {
    if (!this.db) return;
    return new Promise((resolve, reject) => {
      const tx = this.db!.transaction(this.STORE, 'readwrite');
      const req = tx.objectStore(this.STORE).clear();
      req.onsuccess = () => resolve();
      req.onerror = () => reject(req.error);
    });
  }
}
