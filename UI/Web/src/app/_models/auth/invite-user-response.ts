export interface InviteUserResponse {
    /**
     * Link to register new user
     */
    emailLink: string;
    /**
     * If an email was sent to the invited user
     */
    emailSent: boolean;
   /**
   * When a user has an invalid email and is attempting to perform a flow.
   */
   invalidEmail: boolean;
}
