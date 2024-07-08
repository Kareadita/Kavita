export interface SmtpConfig {
  senderAddress: string;
  senderDisplayName: string;
  userName: string;
  password: string;
  host: string;
  port: number;
  enableSsl: boolean;
  sizeLimit: number;
  customizedTemplates: boolean;
}
