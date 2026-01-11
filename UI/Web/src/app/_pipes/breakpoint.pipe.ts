import {Pipe, PipeTransform} from '@angular/core';
import {translate} from "@jsverse/transloco";
import {Breakpoint} from "../_services/breakpoint.service";

@Pipe({
  name: 'breakpoint'
})
export class BreakpointPipe implements PipeTransform {

  transform(value: Breakpoint): string {
    const v = parseInt(value + '', 10) as Breakpoint;
    if (parseInt(value + '', 10) == 0) return translate('breakpoint-pipe.never');

    switch (v) {
      case Breakpoint.Mobile:
        return translate('breakpoint-pipe.mobile');
      case Breakpoint.Tablet:
        return translate('breakpoint-pipe.tablet');
      case Breakpoint.Desktop:
        return translate('breakpoint-pipe.desktop');
    }
    throw new Error("unknown breakpoint value: " + value);
  }

}
