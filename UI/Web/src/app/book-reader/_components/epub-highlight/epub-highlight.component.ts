import {Component, computed, input, model} from '@angular/core';

export type HighlightColor = 'blue' | 'green';

@Component({
  selector: 'app-epub-highlight',
  imports: [],
  templateUrl: './epub-highlight.component.html',
  styleUrl: './epub-highlight.component.scss'
})
export class EpubHighlightComponent {
  showHighlight = model<boolean>(false);
  color = input<HighlightColor>('blue');

  highlightClasses = computed(() => {
    const baseClass = 'epub-highlight';

    if (!this.showHighlight()) {
      return baseClass;
    }

    const colorClass = `epub-highlight-${this.color()}`;
    return `${baseClass} ${colorClass}`;
  });


  toggleHighlight() {
    this.showHighlight.set(!this.showHighlight());
  }
}
