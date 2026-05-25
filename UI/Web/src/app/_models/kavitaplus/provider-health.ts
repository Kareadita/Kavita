import {ScrobbleProvider} from '../../_services/scrobbling.service';

export enum KavitaPlusProviderHealthStatus {
  Unknown = 0,
  Operational = 1,
  Degraded = 2,
  Down = 3,
}

export interface KavitaPlusProviderIncidentDto {
  startedAtUtc: string;
  endedAtUtc: string | null;
  type: number;
}

export interface KavitaPlusProviderHealthSnapshotDto {
  provider: ScrobbleProvider;
  avgLatencyMs: number;
  status: KavitaPlusProviderHealthStatus;
  lastIncident: KavitaPlusProviderIncidentDto | null;
}
