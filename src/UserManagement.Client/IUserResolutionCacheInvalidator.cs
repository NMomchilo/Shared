namespace UserManagement.Client;

public interface IUserResolutionCacheInvalidator
{
    void ObserveRevision(long revision);
}
