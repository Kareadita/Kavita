import {inject, Pipe, PipeTransform} from '@angular/core';
import {BreakpointService} from '../_services/breakpoint.service';

@Pipe({ name: 'truncate', standalone: true, pure: false })
export class TruncatePipe implements PipeTransform {
  private readonly breakpointService = inject(BreakpointService);

  transform(value: string | null | undefined, length = 35, always: boolean = false): string {
    if (!value) return value ?? '';
    if (!this.breakpointService.isMobileOrBelow() && !always) return value;
    return value.length > length ? `${value.slice(0, length).trim()}…` : value;
  }
}
