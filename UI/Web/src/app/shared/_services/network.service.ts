import {computed, Injectable, signal} from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class NetworkService {
  readonly connectionType = signal<'wifi' | 'cellular' | 'unknown'>('unknown');

  /** 25 MB on cellular, 100 MB on wifi/unknown */
  readonly sizeWarningThreshold = computed(() =>
    this.connectionType() === 'cellular' ? 26_214_400 : 104_857_600
  );

  constructor() {
    const conn = (navigator as any).connection;
    if (!conn) return;

    const update = () => {
      const type: string = conn.type ?? '';
      if (type === 'cellular' || type === 'wimax') {
        this.connectionType.set('cellular');
      } else if (type === 'wifi' || type === 'ethernet') {
        this.connectionType.set('wifi');
      } else {
        this.connectionType.set('unknown');
      }
    };
    update();
    conn.addEventListener('change', update);
  }
}
