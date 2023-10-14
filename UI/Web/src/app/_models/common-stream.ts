export interface CommonStream {
  id: number;
  name: string;
  isProvided: boolean;
  order: number;
  visible: boolean;
  smartFilterEncoded?: string;
}
