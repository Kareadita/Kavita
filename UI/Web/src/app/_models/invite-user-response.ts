export interface InviteUserResponse {
    /**
     * Link to register new user
     */
    emailLink: string;
    /**
     * If an email was sent to the invited user
     */
    emailSent: boolean;
}