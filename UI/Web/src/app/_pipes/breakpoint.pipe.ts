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
        return translate('preferences.breakpoints.never');
      case UserBreakpoint.Mobile:
        return translate('preferences.breakpoints.mobile');
      case UserBreakpoint.Tablet:
        return translate('preferences.breakpoints.tablet');
      case UserBreakpoint.Desktop:
        return translate('preferences.breakpoints.desktop');
    }
    throw new Error("unknown breakpoint value: " + value);
  }

}
