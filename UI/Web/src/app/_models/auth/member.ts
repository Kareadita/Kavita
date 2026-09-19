import {AgeRestriction} from '../metadata/age-restriction';
import {Library} from '../library/library';
import {IdentityProvider} from "../user/user";
import {Role} from "../../_services/account.service";

export interface Member {
  id: number;
  username: string;
  email: string;
  lastActive: string; // datetime
  lastActiveUtc: string; // datetime
  created: string; // datetime
  createdUtc: string; // datetime
  roles: Role[];
  libraries: Library[];
  ageRestriction: AgeRestriction;
  isPending: boolean;
  identityProvider: IdentityProvider;
}

