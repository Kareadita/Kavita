import {CblRepoItem} from "./cbl-repo-item";
import {GithubRateLimit} from "../../common/github-rate-limit";

export interface CblRepoBrowseResult {
  items: CblRepoItem[];
  rateLimit: GithubRateLimit;
  fromCache: boolean;
}
