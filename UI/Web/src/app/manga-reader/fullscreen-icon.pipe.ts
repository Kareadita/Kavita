import { Pipe, PipeTransform } from '@angular/core';

/**
 * Returns the icon for the given state of fullscreen mode
 */
@Pipe({
  name: 'fullscreenIcon'
})
export class FullscreenIconPipe implements PipeTransform {

  transform(isFullscreen: boolean): string {
    return isFullscreen ? 'fa-compress-alt' : 'fa-expand-alt';
  }

}
