namespace NebulaPanel.Domain.Enums;

public enum UserActivityType
{
    Login,
    Logout,
    FailedLogin,
    PasswordChanged,
    PasswordResetByAdmin,
    ProfileUpdated,
    RoleAssigned,
    RoleRemoved,
    AccountEnabled,
    AccountDisabled,
    AccountCreated,
    AccountDeleted
}
