import {AgeRestriction} from '../metadata/age-restriction';
import {Library} from '../library/library';
import {UserOwner} from "../user";

export interface Member {
  id: number;
  username: string;
  email: string;
  lastActive: string; // datetime
  lastActiveUtc: string; // datetime
  created: string; // datetime
  createdUtc: string; // datetime
  roles: string[];
  libraries: Library[];
  ageRestriction: AgeRestriction;
  isPending: boolean;
  owner: UserOwner;
}
