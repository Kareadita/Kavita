import {AgeRestriction} from "../metadata/age-restriction";

export interface UpdateUserRequest {
  email: string,
  roles: Array<string>,
  libraries: Array<number>,
  userId: number,
  ageRestriction: AgeRestriction
}
