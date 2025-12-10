import {ResolveFn} from '@angular/router';
import {inject} from "@angular/core";
import {MemberService} from "../_services/member.service";
import {MemberInfo} from "../_models/user/member-info";

export const memberInfoResolver: ResolveFn<MemberInfo> = (route, state) => {
  const memberService = inject(MemberService);

  const userId = route.params['userId'] || route.parent?.params['userId'];
  return memberService.getMemberInfo(userId);
};
