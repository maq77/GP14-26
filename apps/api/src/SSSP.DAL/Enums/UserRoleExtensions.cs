namespace SSSP.DAL.Enums
{
    public static class UserRoleExtensions
    {
        public static string ToIdentityRoleName(this UserRole role)
            => role.ToString();
    }
}
