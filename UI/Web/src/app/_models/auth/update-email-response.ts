export interface UpdateEmailResponse {
    /**
     * Did the user not have an existing email
     */
    hadNoExistingEmail: boolean;
    /**
     * Was an email sent (ie is this server accessible)
     */
    emailSent: boolean;
}