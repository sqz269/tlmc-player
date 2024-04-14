namespace KeycloakAuthProvider.Identity
{
    public class UserClaim
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }

        public UserClaim(Guid userId, string username)
        {
            UserId = userId;
            Username = username;
        }
    }

}
