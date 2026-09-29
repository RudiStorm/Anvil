namespace Anvil;

public sealed class AnvilUnauthorizedException : Exception
{
    public AnvilUnauthorizedException()
        : base("Authentication is required.")
    {
    }
}

public sealed class AnvilForbiddenException : Exception
{
    public AnvilForbiddenException()
        : base("The current user is not authorized for this resource.")
    {
    }
}
