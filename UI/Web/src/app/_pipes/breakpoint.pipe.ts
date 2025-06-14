import {Pipe, PipeTransform} from '@angular/core';
import {translate} from "@jsverse/transloco";
import {UserBreakpoint} from "../shared/_services/utility.service";

@Pipe({
  name: 'breakpoint'
})
export class BreakpointPipe implements PipeTransform {

  transform(value: UserBreakpoint): string {
    const v = parseInt(value + '', 10) as UserBreakpoint;
    switch (v) {
      case UserBreakpoint.Never:
        return translate('breakpoint-pipe.never');
      case UserBreakpoint.Mobile:
        return translate('breakpoint-pipe.mobile');
      case UserBreakpoint.Tablet:
        return translate('breakpoint-pipe.tablet');
      case UserBreakpoint.Desktop:
        return translate('breakpoint-pipe.desktop');
    }
    throw new Error("unknown breakpoint value: " + value);
  }

}
