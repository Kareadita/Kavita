export interface SmtpConfig {
  senderAddress: string;
  senderDisplayName: string;
  userName: string;
  password: string;
  host: string;
  port: number;
  enableSsl: boolean;
  isBodyHtml: boolean;
  allowSendTo?: boolean;
  sizeLimit: number;
}
