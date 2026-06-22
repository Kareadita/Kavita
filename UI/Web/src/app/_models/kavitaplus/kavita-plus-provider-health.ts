import {ScrobbleProvider} from '../../_services/scrobbling.service';

export enum KavitaPlusProviderHealthStatus {
  Unknown = 0,
  Operational = 1,
  Degraded = 2,
  Down = 3,
}

export interface KavitaPlusProviderIncident {
  startedAtUtc: string;
  endedAtUtc: string | null;
  type: number;
}

export interface KavitaPlusProviderHealthSnapshot {
  provider: ScrobbleProvider;
  avgLatencyMs: number;
  status: KavitaPlusProviderHealthStatus;
  lastIncident: KavitaPlusProviderIncident | null;
}
