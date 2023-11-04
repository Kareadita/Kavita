import { Pipe, PipeTransform } from '@angular/core';
import { LayoutMode } from '../manga-reader/_models/layout-mode';

@Pipe({
  name: 'layoutModeIcon',
  standalone: true,
})
export class LayoutModeIconPipe implements PipeTransform {

  transform(layoutMode: LayoutMode): string {
    switch (layoutMode) {
      case LayoutMode.Single:
        return 'none';
      case LayoutMode.Double:
        return 'double';
      case LayoutMode.DoubleReversed:
        return 'double-reversed';
      case LayoutMode.DoubleNoCover:
        return 'double'; // TODO: Validate
    }
  }

}
